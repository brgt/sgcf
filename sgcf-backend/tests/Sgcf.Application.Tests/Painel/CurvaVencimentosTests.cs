using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
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

/// <summary>
/// Testes unitários para <see cref="GetCurvaVencimentosQueryHandler"/>.
/// Todos os mocks de cotação retornam null (BRL apenas) para simplificar a aritmética.
/// </summary>
[Trait("Category", "Slow")]
public sealed class CurvaVencimentosTests
{
    // Instante fixo: 2026-05-21 09:00 UTC → data BRT = 2026-05-21
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 21, 9, 0);
    private static readonly LocalDate Hoje = new(2026, 5, 21);

    // ── helpers ────────────────────────────────────────────────────────────────

    private static GetCurvaVencimentosQueryHandler CriarHandler(
        IContratoRepository contratoRepo,
        IEventoCronogramaRepository cronogramaRepo)
    {
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(default, default).ReturnsForAnyArgs((Money?)null);

        ICotacaoFxRepository cotacaoFxRepo = Substitute.For<ICotacaoFxRepository>();
        cotacaoFxRepo.GetMaisRecenteAsync(default, default, default, default)
                     .ReturnsForAnyArgs((CotacaoFx?)null);

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        return new GetCurvaVencimentosQueryHandler(
            contratoRepo, cronogramaRepo, spotCache, cotacaoFxRepo, clock);
    }

    /// <summary>
    /// Cria um contrato simples em BRL para testes que não precisam de FX.
    /// </summary>
    private static Contrato CriarContrato(Guid bancoId, string numero = "TEST-001")
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        return Contrato.Criar(
            numeroExterno: numero,
            bancoId: bancoId,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 2),
            dataVencimento: new LocalDate(2028, 12, 31),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2028, 12, 31));
    }

    /// <summary>
    /// Cria um evento de cronograma do tipo Principal em BRL.
    /// </summary>
    private static EventoCronograma CriarEventoPrincipal(
        Guid contratoId,
        LocalDate dataPrevista,
        decimal valorBrl,
        short numeroEvento = 1)
    {
        return EventoCronograma.Criar(
            contratoId: contratoId,
            numeroEvento: numeroEvento,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: dataPrevista,
            valorMoedaOriginal: new Money(valorBrl, Moeda.Brl));
    }

    // ── testes ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Com granularidade Mes, cada evento em mês diferente deve gerar um bucket
    /// com label no formato YYYY-MM.
    /// </summary>
    [Fact]
    public async Task Handle_GranularidadeMes_RetornaBucketsComLabelYyyyMm()
    {
        // Arrange — três eventos em meses consecutivos
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);

        EventoCronograma e1 = CriarEventoPrincipal(contrato.Id, new LocalDate(2026, 6, 30), 100_000m, 1);
        EventoCronograma e2 = CriarEventoPrincipal(contrato.Id, new LocalDate(2026, 7, 31), 200_000m, 2);
        EventoCronograma e3 = CriarEventoPrincipal(contrato.Id, new LocalDate(2026, 8, 31), 300_000m, 3);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosNoPeriodoAsync(
            Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .ReturnsForAnyArgs(new List<EventoCronograma> { e1, e2, e3 }.AsReadOnly());

        GetCurvaVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<CurvaVencimentosDto> envelope = await handler.Handle(
            new GetCurvaVencimentosQuery(12, GranularidadeHorizonte.Mes),
            CancellationToken.None);

        CurvaVencimentosDto resultado = envelope.Data;

        // Assert
        resultado.Buckets.Should().HaveCount(3, "três meses distintos");
        resultado.Buckets.Select(b => b.Label).Should()
            .BeEquivalentTo(["2026-06", "2026-07", "2026-08"],
                "labels no formato YYYY-MM em ordem cronológica");

        resultado.Buckets.Single(b => b.Label == "2026-06").TotalBrl.Should().Be(100_000m);
        resultado.Buckets.Single(b => b.Label == "2026-07").TotalBrl.Should().Be(200_000m);
        resultado.Buckets.Single(b => b.Label == "2026-08").TotalBrl.Should().Be(300_000m);
    }

    /// <summary>
    /// Com granularidade Trimestre, eventos em Jan, Feb e Mar devem ser agrupados em Q1;
    /// eventos em Abr e Jun devem ir para Q2.
    /// </summary>
    [Fact]
    public async Task Handle_GranularidadeTrimestre_AgrupaMesesNoTrimestre()
    {
        // Arrange — Jan + Feb + Mar = Q1 (600); Abr + Jun = Q2 (500)
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);

        EventoCronograma e1 = CriarEventoPrincipal(contrato.Id, new LocalDate(2027, 1, 31), 100_000m, 1);
        EventoCronograma e2 = CriarEventoPrincipal(contrato.Id, new LocalDate(2027, 2, 28), 200_000m, 2);
        EventoCronograma e3 = CriarEventoPrincipal(contrato.Id, new LocalDate(2027, 3, 31), 300_000m, 3);
        EventoCronograma e4 = CriarEventoPrincipal(contrato.Id, new LocalDate(2027, 4, 30), 250_000m, 4);
        EventoCronograma e5 = CriarEventoPrincipal(contrato.Id, new LocalDate(2027, 6, 30), 250_000m, 5);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosNoPeriodoAsync(
            Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .ReturnsForAnyArgs(new List<EventoCronograma> { e1, e2, e3, e4, e5 }.AsReadOnly());

        GetCurvaVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<CurvaVencimentosDto> envelope = await handler.Handle(
            new GetCurvaVencimentosQuery(24, GranularidadeHorizonte.Trimestre),
            CancellationToken.None);

        CurvaVencimentosDto resultado = envelope.Data;

        // Assert
        resultado.Buckets.Should().HaveCount(2, "apenas Q1 e Q2 têm eventos");

        BucketVencimentoDto q1 = resultado.Buckets.Single(b => b.Label == "2027-Q1");
        q1.TotalBrl.Should().Be(600_000m, "Jan + Feb + Mar = 600k");

        BucketVencimentoDto q2 = resultado.Buckets.Single(b => b.Label == "2027-Q2");
        q2.TotalBrl.Should().Be(500_000m, "Abr + Jun = 500k");
    }

    /// <summary>
    /// Com granularidade Ano, eventos em 2026 e 2027 devem gerar exatamente dois buckets.
    /// </summary>
    [Fact]
    public async Task Handle_GranularidadeAno_AgrupaTodosEventosDoAno()
    {
        // Arrange — dois eventos em 2026 e um em 2027
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);

        EventoCronograma e1 = CriarEventoPrincipal(contrato.Id, new LocalDate(2026, 9, 30), 400_000m, 1);
        EventoCronograma e2 = CriarEventoPrincipal(contrato.Id, new LocalDate(2026, 12, 31), 100_000m, 2);
        EventoCronograma e3 = CriarEventoPrincipal(contrato.Id, new LocalDate(2027, 6, 30), 300_000m, 3);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosNoPeriodoAsync(
            Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .ReturnsForAnyArgs(new List<EventoCronograma> { e1, e2, e3 }.AsReadOnly());

        GetCurvaVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<CurvaVencimentosDto> envelope = await handler.Handle(
            new GetCurvaVencimentosQuery(24, GranularidadeHorizonte.Ano),
            CancellationToken.None);

        CurvaVencimentosDto resultado = envelope.Data;

        // Assert
        resultado.Buckets.Should().HaveCount(2, "dois anos distintos");

        resultado.Buckets.Single(b => b.Label == "2026").TotalBrl.Should().Be(500_000m,
            "400k + 100k = 500k em 2026");
        resultado.Buckets.Single(b => b.Label == "2027").TotalBrl.Should().Be(300_000m,
            "300k em 2027");
    }

    /// <summary>
    /// TotalBrl da CurvaVencimentosDto deve ser a soma exata dos TotalBrl de todos os buckets.
    /// </summary>
    [Fact]
    public async Task Handle_TotalBrlDoEnvelope_IgualSomaDoBuckets()
    {
        // Arrange — três buckets mensais
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);

        EventoCronograma e1 = CriarEventoPrincipal(contrato.Id, new LocalDate(2026, 6, 30), 111_111m, 1);
        EventoCronograma e2 = CriarEventoPrincipal(contrato.Id, new LocalDate(2026, 7, 31), 222_222m, 2);
        EventoCronograma e3 = CriarEventoPrincipal(contrato.Id, new LocalDate(2026, 8, 31), 333_333m, 3);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosNoPeriodoAsync(
            Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .ReturnsForAnyArgs(new List<EventoCronograma> { e1, e2, e3 }.AsReadOnly());

        GetCurvaVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<CurvaVencimentosDto> envelope = await handler.Handle(
            new GetCurvaVencimentosQuery(12, GranularidadeHorizonte.Mes),
            CancellationToken.None);

        CurvaVencimentosDto resultado = envelope.Data;

        // Assert — invariante: TotalBrl == soma dos buckets
        decimal somaBuckets = resultado.Buckets.Sum(b => b.TotalBrl);
        resultado.TotalBrl.Should().Be(somaBuckets,
            "TotalBrl do envelope deve ser idêntico à soma dos buckets");
    }

    /// <summary>
    /// Um meses inválido (ex: 99) deve ser normalizado para 12 pelo handler
    /// sem lançar exceção.
    /// </summary>
    [Fact]
    public async Task Handle_MesesInvalido99_UsaDefault12SemExcecao()
    {
        // Arrange — um evento dentro de 12 meses, outro além de 12 meses
        // O repositório é mockado para responder apenas com o que o caller passa nas datas;
        // verificamos que o handler não lança e que a query chega ao repositório.
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);

        EventoCronograma eventoProximo = CriarEventoPrincipal(
            contrato.Id, Hoje.PlusMonths(6), 500_000m, 1);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();

        // Captura as datas passadas para verificar que o handler usa 12 meses
        LocalDate dataInicioCapturada = default;
        LocalDate dataFimCapturada = default;

        cronogramaRepo.ListAbertosNoPeriodoAsync(
            Arg.Do<LocalDate>(d => dataInicioCapturada = d),
            Arg.Do<LocalDate>(d => dataFimCapturada = d),
            Arg.Any<IReadOnlyCollection<Guid>>(),
            default)
            .ReturnsForAnyArgs(new List<EventoCronograma> { eventoProximo }.AsReadOnly());

        GetCurvaVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act — meses=99 deve ser normalizado para 12 sem exceção
        Func<Task> act = () => handler.Handle(
            new GetCurvaVencimentosQuery(99, GranularidadeHorizonte.Mes),
            CancellationToken.None);

        await act.Should().NotThrowAsync("meses inválido deve usar default 12");

        // O handler deve ter passado Hoje + 12 meses como dataFim
        dataFimCapturada.Should().Be(Hoje.PlusMonths(12),
            "meses=99 (inválido) deve resultar em horizonte de 12 meses");
    }

    /// <summary>
    /// O breakdown PorModalidade dentro de cada bucket deve somar o TotalBrl do bucket.
    /// </summary>
    [Fact]
    public async Task Handle_BreakdownModalidade_SomaCoincidicComTotalBucket()
    {
        // Arrange — dois contratos de modalidades distintas, ambos com vencimento no mesmo mês
        Guid bancoId = Guid.NewGuid();

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        Contrato contratoCapital = Contrato.Criar(
            numeroExterno: "CAPITAL-001",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 2),
            dataVencimento: new LocalDate(2028, 12, 31),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2028, 12, 31));

        Contrato contratoNce = Contrato.Criar(
            numeroExterno: "NCE-001",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Nce,
            valorPrincipal: new Money(2_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 2),
            dataVencimento: new LocalDate(2028, 12, 31),
            taxaAa: Percentual.DeFracao(0.09m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2028, 12, 31));

        EventoCronograma eventoCapital = CriarEventoPrincipal(
            contratoCapital.Id, new LocalDate(2026, 9, 30), 300_000m);
        EventoCronograma eventoNce = CriarEventoPrincipal(
            contratoNce.Id, new LocalDate(2026, 9, 30), 700_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contratoCapital, contratoNce }.AsReadOnly());

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo.ListAbertosNoPeriodoAsync(
            Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .ReturnsForAnyArgs(new List<EventoCronograma> { eventoCapital, eventoNce }.AsReadOnly());

        GetCurvaVencimentosQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<CurvaVencimentosDto> envelope = await handler.Handle(
            new GetCurvaVencimentosQuery(12, GranularidadeHorizonte.Mes),
            CancellationToken.None);

        CurvaVencimentosDto resultado = envelope.Data;

        // Assert
        BucketVencimentoDto bucket = resultado.Buckets.Single(b => b.Label == "2026-09");
        bucket.TotalBrl.Should().Be(1_000_000m);

        bucket.PorModalidade.Should().HaveCount(2, "dois contratos de modalidades distintas");

        decimal somaModalidades = bucket.PorModalidade.Sum(m => m.ValorBrl);
        somaModalidades.Should().Be(bucket.TotalBrl,
            "soma das modalidades deve coincidir com o total do bucket");

        bucket.PorModalidade.Single(m => m.Modalidade == "CapitalDeGiro").ValorBrl.Should().Be(300_000m);
        bucket.PorModalidade.Single(m => m.Modalidade == "Nce").ValorBrl.Should().Be(700_000m);
    }
}
