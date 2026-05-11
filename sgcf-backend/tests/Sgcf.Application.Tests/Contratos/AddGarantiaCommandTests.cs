using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Application.Tests.Contratos;

/// <summary>
/// Testes unitários do handler <see cref="AddGarantiaCommandHandler"/> usando mocks (NSubstitute).
/// Verifica validações bloqueantes e alertas não-bloqueantes sem tocar banco de dados.
/// </summary>
[Trait("Category", "Domain")]
public sealed class AddGarantiaCommandTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 11, 12, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static Contrato CriarContratoBrl(
        decimal valorPrincipal = 1_000_000m,
        LocalDate? dataVencimento = null)
    {
        IClock clock = CriarClock();
        return Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Nce,
            valorPrincipal: new Money(valorPrincipal, Moeda.Brl),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: dataVencimento ?? new LocalDate(2028, 1, 15),
            taxaAa: Percentual.De(12m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock);
    }

    private static Contrato CriarContratoUsd()
    {
        IClock clock = CriarClock();
        return Contrato.Criar(
            numeroExterno: "FIN-2026-002",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(500_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2028, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    private static (IContratoRepository, IGarantiaRepository, AddGarantiaCommandHandler) CriarHandler(
        Contrato contrato,
        decimal totalFaturamentoCartao = 0m)
    {
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);

        IGarantiaRepository garantiaRepo = Substitute.For<IGarantiaRepository>();
        garantiaRepo
            .GetTotalPercentualFaturamentoCartaoAsync(contrato.Id, Arg.Any<CancellationToken>())
            .Returns(totalFaturamentoCartao);
        garantiaRepo.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        AddGarantiaCommandHandler handler = new(contratoRepo, garantiaRepo, CriarClock());
        return (contratoRepo, garantiaRepo, handler);
    }

    private static AddGarantiaCommand ComandoCdbValido(Guid contratoId, DateOnly? vencimentoCdb = null) =>
        new(
            ContratoId: contratoId,
            Tipo: "CdbCativo",
            ValorBrl: 200_000m,
            DataConstituicao: new DateOnly(2026, 2, 1),
            DataLiberacaoPrevista: null,
            Observacoes: null,
            CreatedBy: "test",
            Cdb: new AddGarantiaCdbPayload(
                BancoCustodia: "Banco do Brasil",
                NumeroCdb: "CDB-001",
                DataEmissaoCdb: new DateOnly(2026, 2, 1),
                DataVencimentoCdb: vencimentoCdb ?? new DateOnly(2028, 2, 1),
                RendimentoAaPct: 13.5m,
                PercentualCdiPct: null,
                TaxaIrrfAplicacaoPct: null));

    // ── Bloqueante: constituição após vencimento do contrato ──────────────────

    [Fact]
    public async Task AddGarantia_DataConstituicaoAposVencimentoContrato_LancaArgumentException()
    {
        // Arrange
        Contrato contrato = CriarContratoBrl(dataVencimento: new LocalDate(2027, 1, 15));
        (_, _, AddGarantiaCommandHandler handler) = CriarHandler(contrato);

        AddGarantiaCommand command = new(
            ContratoId: contrato.Id,
            Tipo: "Aval",
            ValorBrl: 100_000m,
            DataConstituicao: new DateOnly(2027, 2, 1),   // APÓS vencimento
            DataLiberacaoPrevista: null,
            Observacoes: null,
            CreatedBy: "test",
            Aval: new AddGarantiaAvalPayload("PJ", "Empresa Ltda", "12.345.678/0001-00", 100_000m, null));

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*após o vencimento do contrato*");
    }

    // ── Bloqueante: CDB vence antes do contrato ───────────────────────────────

    [Fact]
    public async Task AddGarantia_CdbCativo_VencimentoAnteriorAoContrato_LancaArgumentException()
    {
        // Arrange — contrato vence em 2028-01-15, CDB vence antes em 2027-06-01
        Contrato contrato = CriarContratoBrl(dataVencimento: new LocalDate(2028, 1, 15));
        (_, _, AddGarantiaCommandHandler handler) = CriarHandler(contrato);

        AddGarantiaCommand command = ComandoCdbValido(contrato.Id, vencimentoCdb: new DateOnly(2027, 6, 1));

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*vencimento do CDB cativo*");
    }

    // ── Bloqueante: soma de % faturamento cartão > 100% ──────────────────────

    [Fact]
    public async Task AddGarantia_RecebiveisCartao_PctSomaMaisQue100_LancaArgumentException()
    {
        // Arrange — já existe 80% comprometido (como fração 0.80), novo pedido de 30% excederia
        Contrato contrato = CriarContratoBrl();
        (_, _, AddGarantiaCommandHandler handler) = CriarHandler(
            contrato, totalFaturamentoCartao: 0.80m);

        AddGarantiaCommand command = new(
            ContratoId: contrato.Id,
            Tipo: "RecebiveisCartao",
            ValorBrl: 200_000m,
            DataConstituicao: new DateOnly(2026, 3, 1),
            DataLiberacaoPrevista: null,
            Observacoes: null,
            CreatedBy: "test",
            Recebiveis: new AddGarantiaRecebiveisPayload(
                OperadoraCartao: "Cielo",
                TipoRecebivel: "VISTA",
                PercentualFaturamentoPct: 30m,   // 80% + 30% = 110% > 100%
                ValorMedioMensalBrl: null,
                PrazoRecebimentoDias: 30,
                TermoCessaoUrl: null));

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*excederia 100%*");
    }

    // ── Alerta: cobertura < 100% do principal ────────────────────────────────

    [Fact]
    public async Task AddGarantia_CoberturaAbaixoDe100Pct_EmiteAlerta()
    {
        // Arrange — contrato BRL 1M, garantia BRL 200k = 20% de cobertura
        Contrato contrato = CriarContratoBrl(valorPrincipal: 1_000_000m);
        (_, _, AddGarantiaCommandHandler handler) = CriarHandler(contrato);

        AddGarantiaCommand command = ComandoCdbValido(contrato.Id);

        // Act — não deve lançar; retorna DTO com alerta
        GarantiaDto result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Alertas.Should().ContainSingle(a => a.Contains("20,0%") || a.Contains("20.0%") || a.Contains("20"));
        result.Status.Should().Be("Ativa");
    }

    // ── Alerta: moeda estrangeira sem NDF ────────────────────────────────────

    [Fact]
    public async Task AddGarantia_MoedaEstrangeiraSemNDF_EmiteAlertaCritico()
    {
        // Arrange — contrato em USD
        Contrato contrato = CriarContratoUsd();
        (_, _, AddGarantiaCommandHandler handler) = CriarHandler(contrato);

        AddGarantiaCommand command = new(
            ContratoId: contrato.Id,
            Tipo: "Sblc",
            ValorBrl: 500_000m,
            DataConstituicao: new DateOnly(2026, 2, 1),
            DataLiberacaoPrevista: null,
            Observacoes: null,
            CreatedBy: "test",
            Sblc: new AddGarantiaSblcPayload(
                BancoEmissor: "JP Morgan",
                PaisEmissor: "US",
                SwiftCode: "CHASUSY",
                ValidadeDias: 730,
                ComissaoAaPct: 1.5m,
                NumeroSblc: "SBLC-001"));

        // Act
        GarantiaDto result = await handler.Handle(command, CancellationToken.None);

        // Assert — alerta crítico deve estar presente
        result.Alertas.Should().Contain(a => a.Contains("NDF"));
        result.Alertas.Should().Contain(a => a.Contains("CRÍTICO") || a.Contains("CRITICO") || a.Contains("estrangeira"));
    }

    // ── Happy path: garantia criada com sucesso ───────────────────────────────

    [Fact]
    public async Task AddGarantia_DadosValidos_RetornaGarantiaDtoComStatusAtiva()
    {
        // Arrange
        Contrato contrato = CriarContratoBrl();
        (_, IGarantiaRepository garantiaRepo, AddGarantiaCommandHandler handler) = CriarHandler(contrato);

        AddGarantiaCommand command = ComandoCdbValido(contrato.Id);

        // Act
        GarantiaDto result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.ContratoId.Should().Be(contrato.Id);
        result.Tipo.Should().Be("CdbCativo");
        result.Status.Should().Be("Ativa");
        result.ValorBrl.Should().Be(200_000m);
        result.Cdb.Should().NotBeNull();

        // Both Add calls should have been made
        garantiaRepo.Received(1).Add(Arg.Any<Garantia>());
        garantiaRepo.Received(1).AddCdbCativoDetail(Arg.Any<GarantiaCdbCativoDetail>());
        await garantiaRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
