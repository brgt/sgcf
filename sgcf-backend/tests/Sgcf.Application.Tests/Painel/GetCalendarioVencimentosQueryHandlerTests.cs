using System.Collections.ObjectModel;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
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
        ICdiSnapshotRepository cdiRepo)
    {
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(default, default).ReturnsForAnyArgs((Money?)null);

        ICotacaoFxRepository cotacaoFxRepo = Substitute.For<ICotacaoFxRepository>();
        cotacaoFxRepo.GetMaisRecenteAsync(default, default, default, default)
                     .ReturnsForAnyArgs((CotacaoFx?)null);

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        return new GetCalendarioVencimentosQueryHandler(
            contratoRepo, cronogramaRepo, spotCache, cotacaoFxRepo, cdiRepo, clock);
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
}
