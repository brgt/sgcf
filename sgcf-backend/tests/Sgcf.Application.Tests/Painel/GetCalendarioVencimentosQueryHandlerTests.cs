using System.Collections.ObjectModel;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Xunit;

namespace Sgcf.Application.Tests.Painel;

[Trait("Category", "Unit")]
public sealed class GetCalendarioVencimentosQueryHandlerTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 18, 9, 0);
    private static readonly LocalDate Hoje = new(2026, 5, 18);

    // ── helpers ────────────────────────────────────────────────────────────────

    private static GetCalendarioVencimentosQueryHandler CriarHandler(
        IContratoRepository contratoRepo,
        IEventoCronogramaRepository cronogramaRepo,
        ICdiSnapshotRepository cdiRepo,
        IBancoRepository? bancoRepo = null)
    {
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(default, default).ReturnsForAnyArgs((Money?)null);

        ICotacaoFxRepository cotacaoFxRepo = Substitute.For<ICotacaoFxRepository>();
        cotacaoFxRepo.GetMaisRecenteAsync(default, default, default, default)
                     .ReturnsForAnyArgs((CotacaoFx?)null);

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        IBancoRepository resolvedBancoRepo;
        if (bancoRepo is not null)
        {
            resolvedBancoRepo = bancoRepo;
        }
        else
        {
            // Banco repo vazio por padrão — testes que não precisam de banco não quebram
            resolvedBancoRepo = Substitute.For<IBancoRepository>();
            resolvedBancoRepo.ListAllAsync(default).ReturnsForAnyArgs(
                new List<Sgcf.Domain.Bancos.Banco>().AsReadOnly());
        }

        return new GetCalendarioVencimentosQueryHandler(
            contratoRepo, cronogramaRepo, spotCache, cotacaoFxRepo, cdiRepo, resolvedBancoRepo, clock);
    }

    /// <summary>
    /// Cria um Banco de teste com apelido e código COMPE definidos.
    /// </summary>
    private static Banco CriarBanco(string codigoCompe, string apelido)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return Banco.Criar(codigoCompe, $"Banco {apelido} S.A.", apelido, clock);
    }

    private static Contrato CriarContratoCarencia(Guid bancoId)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        return Contrato.Criar(
            numeroExterno: "CEF-CCB-TEST-001",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(5_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2026, 2, 26),
            dataVencimento: new LocalDate(2029, 2, 26),
            taxaAa: Percentual.DeFracao(0.0317m),   // spread: 0,26% a.m. ≈ 3,17% a.a.
            baseCalculo: BaseCalculo.Dias252,
            clock: clock,
            periodicidade: Periodicidade.Bullet,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2029, 2, 26));
    }

    private static ReadOnlyCollection<EventoCronograma> CriarEventosCarencia(Guid contratoId)
    {
        // 6 parcelas de Juros com valor 0 (CDI-indexado) — Mar a Ago/2026
        LocalDate[] datas =
        [
            new(2026, 3, 26), new(2026, 4, 26), new(2026, 5, 26),
            new(2026, 6, 26), new(2026, 7, 26), new(2026, 8, 26),
        ];

        return datas.Select((d, i) =>
            EventoCronograma.Criar(
                contratoId: contratoId,
                numeroEvento: (short)(i + 1),
                tipo: TipoEventoCronograma.Juros,
                dataPrevista: d,
                valorMoedaOriginal: new Money(0m, Moeda.Brl)))
            .ToList()
            .AsReadOnly();
    }

    // ── testes ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bug 2026-05-18: parcelas de carência de contrato CDI-indexado retornavam
    /// JurosBrlProjetado = null quando cdiAnualPct não era informado na query.
    /// Após a correção, o handler deve buscar automaticamente o snapshot mais recente
    /// e popular JurosBrlProjetado para cada evento de Juros com valor 0.
    /// </summary>
    [Fact]
    public async Task Handle_ContratoCdiComJurosZeroNaCarencia_PopulaJurosBrlProjetadoAutomaticamente()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();

        Contrato contrato = CriarContratoCarencia(bancoId);
        Guid contratoId = contrato.Id;
        IReadOnlyList<EventoCronograma> eventos = CriarEventosCarencia(contratoId);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosParaAnoAsync(2026, Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(eventos);
        // Sem amortizações de principal antes dos eventos de carência
        cronogramaRepo.ListPrincipaisOrdenadosByContratoIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma>().AsReadOnly());

        IClock clockCdi = Substitute.For<IClock>();
        clockCdi.GetCurrentInstant().Returns(InstanteFixo);
        CdiSnapshot snapshot = CdiSnapshot.Criar(Hoje, 10.75m, clockCdi);

        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        cdiRepo.GetMaisRecenteAsync(Hoje, default).ReturnsForAnyArgs(snapshot);

        GetCalendarioVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo, cdiRepo);

        // Act — sem cdiAnualPct na query (caso do bug)
        CalendarioVencimentosDto resultado = await handler.Handle(
            new GetCalendarioVencimentosQuery(Ano: 2026),
            CancellationToken.None);

        // Assert
        resultado.TaxaCdiUsadaPct.Should().Be(10.75m,
            "o handler deve usar o CDI do snapshot quando não informado na query");

        int[] mesesCarencia = [3, 4, 5, 6, 7, 8];
        foreach (int mes in mesesCarencia)
        {
            MesVencimentoDto mesDto = resultado.Meses.Single(m => m.Mes == mes);

            mesDto.TotalJurosBrlProjetado.Should().NotBeNull(
                $"mês {mes} tem evento de juros CDI e deve ter projeção");
            mesDto.TotalJurosBrlProjetado.Should().BeGreaterThan(0m,
                $"mês {mes}: projeção de juros CDI sobre R$5M deve ser positiva");

            mesDto.Parcelas.Should().ContainSingle();
            VencimentoItemDto parcela = mesDto.Parcelas[0];
            parcela.JurosBrl.Should().Be(0m, "juros realizados são zero durante carência CDI");
            parcela.JurosBrlProjetado.Should().BeGreaterThan(0m,
                $"parcela de {parcela.Data} deve ter juros projetados");
        }
    }

    [Fact]
    public async Task Handle_SemJurosZero_NaoBuscaSnapshot()
    {
        // Arrange — todos os eventos têm valor realizado (não-zero)
        Guid bancoId = Guid.NewGuid();

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        Contrato contrato = Contrato.Criar(
            numeroExterno: "TEST-PREFIXADO-001",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 2),
            dataVencimento: new LocalDate(2026, 12, 31),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2026, 12, 31));

        Guid contratoId = contrato.Id;

        EventoCronograma eventoRealizado = EventoCronograma.Criar(
            contratoId: contratoId,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Juros,
            dataPrevista: new LocalDate(2026, 6, 30),
            valorMoedaOriginal: new Money(50_000m, Moeda.Brl));  // valor não-zero

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosParaAnoAsync(2026, Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma> { eventoRealizado }.AsReadOnly());
        cronogramaRepo.ListPrincipaisOrdenadosByContratoIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma>().AsReadOnly());

        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();

        GetCalendarioVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo, cdiRepo);

        // Act
        CalendarioVencimentosDto resultado = await handler.Handle(
            new GetCalendarioVencimentosQuery(Ano: 2026),
            CancellationToken.None);

        // Assert
        await cdiRepo.DidNotReceiveWithAnyArgs().GetMaisRecenteAsync(default, default);
        resultado.TaxaCdiUsadaPct.Should().BeNull("sem eventos de juros zero, CDI não deve ser buscado");

        MesVencimentoDto junho = resultado.Meses.Single(m => m.Mes == 6);
        junho.Parcelas[0].JurosBrl.Should().Be(50_000m);
        junho.Parcelas[0].JurosBrlProjetado.Should().BeNull();
    }

    [Fact]
    public async Task Handle_JurosZeroMasSnapshotAusente_MantemProjecaoNula()
    {
        // Arrange — snapshot CDI não cadastrado no banco
        Guid bancoId = Guid.NewGuid();

        Contrato contrato = CriarContratoCarencia(bancoId);
        Guid contratoId = contrato.Id;
        IReadOnlyList<EventoCronograma> eventos = CriarEventosCarencia(contratoId);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosParaAnoAsync(2026, Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(eventos);
        cronogramaRepo.ListPrincipaisOrdenadosByContratoIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma>().AsReadOnly());

        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        cdiRepo.GetMaisRecenteAsync(Hoje, default).ReturnsForAnyArgs((CdiSnapshot?)null);

        GetCalendarioVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo, cdiRepo);

        // Act
        CalendarioVencimentosDto resultado = await handler.Handle(
            new GetCalendarioVencimentosQuery(Ano: 2026),
            CancellationToken.None);

        // Assert
        resultado.TaxaCdiUsadaPct.Should().BeNull("sem snapshot disponível, CDI não pode ser aplicado");
        foreach (MesVencimentoDto mes in resultado.Meses.Where(m => m.Parcelas.Count > 0))
        {
            mes.TotalJurosBrlProjetado.Should().BeNull();
            mes.Parcelas[0].JurosBrlProjetado.Should().BeNull();
        }
    }

    // ── Task 1.3 — AD-10: BancoId + BancoApelido em VencimentoItemDto ─────────

    /// <summary>
    /// AD-10 (Task 1.3): cada parcela do calendário deve expor o bancoId do contrato.
    /// </summary>
    [Fact]
    public async Task Handle_RetornaVencimentoItemDto_ComBancoIdPopulado()
    {
        // Arrange — usa banco.Id como bancoId do contrato para que o lookup funcione
        Banco banco = CriarBanco("341", "Itau");
        Guid bancoId = banco.Id;

        Contrato contrato = CriarContratoCarencia(bancoId);
        Guid contratoId = contrato.Id;

        EventoCronograma evento = EventoCronograma.Criar(
            contratoId: contratoId,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: new LocalDate(2026, 6, 30),
            valorMoedaOriginal: new Money(100_000m, Moeda.Brl));

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosParaAnoAsync(2026, Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma> { evento }.AsReadOnly());
        cronogramaRepo.ListPrincipaisOrdenadosByContratoIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma>().AsReadOnly());

        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(default).ReturnsForAnyArgs(
            new List<Banco> { banco }.AsReadOnly());

        GetCalendarioVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo, cdiRepo, bancoRepo);

        // Act
        CalendarioVencimentosDto resultado = await handler.Handle(
            new GetCalendarioVencimentosQuery(Ano: 2026),
            CancellationToken.None);

        // Assert
        VencimentoItemDto parcela = resultado.Meses
            .Single(m => m.Mes == 6).Parcelas.Single();

        parcela.BancoId.Should().Be(bancoId,
            "bancoId deve espelhar o BancoId do contrato (AD-10)");
    }

    /// <summary>
    /// AD-10 (Task 1.3): bancoApelido deve ser preenchido com o Apelido do banco.
    /// </summary>
    [Fact]
    public async Task Handle_RetornaVencimentoItemDto_ComBancoApelidoPopulado()
    {
        // Arrange — usa banco.Id como bancoId do contrato para que o lookup funcione
        Banco banco = CriarBanco("033", "Santander");
        Guid bancoId = banco.Id;

        Contrato contrato = CriarContratoCarencia(bancoId);
        Guid contratoId = contrato.Id;

        EventoCronograma evento = EventoCronograma.Criar(
            contratoId: contratoId,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: new LocalDate(2026, 7, 15),
            valorMoedaOriginal: new Money(50_000m, Moeda.Brl));

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosParaAnoAsync(2026, Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma> { evento }.AsReadOnly());
        cronogramaRepo.ListPrincipaisOrdenadosByContratoIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma>().AsReadOnly());

        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(default).ReturnsForAnyArgs(
            new List<Banco> { banco }.AsReadOnly());

        GetCalendarioVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo, cdiRepo, bancoRepo);

        // Act
        CalendarioVencimentosDto resultado = await handler.Handle(
            new GetCalendarioVencimentosQuery(Ano: 2026),
            CancellationToken.None);

        // Assert
        VencimentoItemDto parcela = resultado.Meses
            .Single(m => m.Mes == 7).Parcelas.Single();

        parcela.BancoApelido.Should().Be("Santander",
            "bancoApelido deve ser o Apelido cadastrado do banco (AD-10)");
    }

    /// <summary>
    /// AD-10 (Task 1.3): múltiplos contratos com bancos diferentes devem preservar
    /// o bancoId/bancoApelido correto em cada parcela.
    /// </summary>
    [Fact]
    public async Task Handle_MultiplosContratosBancosDiferentes_PreserveBancoNoItem()
    {
        // Arrange — dois contratos em bancos distintos, ambos com vencimento no mesmo mês
        // Usa banco.Id como bancoId do contrato para que o lookup funcione
        Banco banco1 = CriarBanco("237", "Bradesco");
        Banco banco2 = CriarBanco("104", "CEF");
        Guid bancoId1 = banco1.Id;
        Guid bancoId2 = banco2.Id;

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        Contrato contrato1 = Contrato.Criar(
            numeroExterno: "BRAD-001",
            bancoId: bancoId1,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 2),
            dataVencimento: new LocalDate(2026, 8, 31),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2026, 8, 31));

        Contrato contrato2 = Contrato.Criar(
            numeroExterno: "CEF-001",
            bancoId: bancoId2,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(2_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 2),
            dataVencimento: new LocalDate(2026, 8, 31),
            taxaAa: Percentual.DeFracao(0.08m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2026, 8, 31));

        EventoCronograma evento1 = EventoCronograma.Criar(
            contratoId: contrato1.Id,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: new LocalDate(2026, 8, 31),
            valorMoedaOriginal: new Money(1_000_000m, Moeda.Brl));

        EventoCronograma evento2 = EventoCronograma.Criar(
            contratoId: contrato2.Id,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: new LocalDate(2026, 8, 31),
            valorMoedaOriginal: new Money(2_000_000m, Moeda.Brl));

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato1, contrato2 }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosParaAnoAsync(2026, Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma> { evento1, evento2 }.AsReadOnly());
        cronogramaRepo.ListPrincipaisOrdenadosByContratoIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma>().AsReadOnly());

        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(default).ReturnsForAnyArgs(
            new List<Banco> { banco1, banco2 }.AsReadOnly());

        GetCalendarioVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo, cdiRepo, bancoRepo);

        // Act
        CalendarioVencimentosDto resultado = await handler.Handle(
            new GetCalendarioVencimentosQuery(Ano: 2026),
            CancellationToken.None);

        // Assert — duas parcelas em agosto, cada uma com seu respectivo banco
        IReadOnlyList<VencimentoItemDto> parcelasAgosto = resultado.Meses
            .Single(m => m.Mes == 8).Parcelas;

        parcelasAgosto.Should().HaveCount(2, "dois contratos distintos no mês");

        VencimentoItemDto parcelaBrad = parcelasAgosto.Single(p => p.ContratoId == contrato1.Id);
        parcelaBrad.BancoId.Should().Be(bancoId1);
        parcelaBrad.BancoApelido.Should().Be("Bradesco");

        VencimentoItemDto parcelaCef = parcelasAgosto.Single(p => p.ContratoId == contrato2.Id);
        parcelaCef.BancoId.Should().Be(bancoId2);
        parcelaCef.BancoApelido.Should().Be("CEF");
    }

    /// <summary>
    /// AD-10 (Task 1.3): quando o banco não é encontrado no repositório
    /// (cenário de dados incompletos), o handler deve retornar o bancoId
    /// do contrato e usar o CodigoCompe como apelido de fallback.
    /// </summary>
    [Fact]
    public async Task Handle_BancoNaoEncontradoNoRepositorio_UsaBancoIdDoContratoComApelidoVazio()
    {
        // Arrange — bancoRepo não possui o banco do contrato (fallback gracioso)
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContratoCarencia(bancoId);
        Guid contratoId = contrato.Id;

        EventoCronograma evento = EventoCronograma.Criar(
            contratoId: contratoId,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: new LocalDate(2026, 9, 26),
            valorMoedaOriginal: new Money(500_000m, Moeda.Brl));

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosParaAnoAsync(2026, Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma> { evento }.AsReadOnly());
        cronogramaRepo.ListPrincipaisOrdenadosByContratoIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
                      .ReturnsForAnyArgs(new List<EventoCronograma>().AsReadOnly());

        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();

        // Repositório vazio — nenhum banco cadastrado
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(default).ReturnsForAnyArgs(
            new List<Banco>().AsReadOnly());

        GetCalendarioVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo, cdiRepo, bancoRepo);

        // Act
        CalendarioVencimentosDto resultado = await handler.Handle(
            new GetCalendarioVencimentosQuery(Ano: 2026),
            CancellationToken.None);

        // Assert — não deve lançar exceção; bancoId do contrato deve ser preservado
        VencimentoItemDto parcela = resultado.Meses.Single(m => m.Mes == 9).Parcelas.Single();

        parcela.BancoId.Should().Be(bancoId,
            "mesmo sem banco no repositório, o bancoId do contrato deve ser preservado");
        parcela.BancoApelido.Should().NotBeNull(
            "fallback deve retornar string não-nula mesmo sem banco encontrado");
    }
}
