using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class RegistrarPropostaCommandHandlerTests
{
    private static RegistrarPropostaCommand CriarComandoUsd(Guid cotacaoId, Guid bancoId) =>
        new(cotacaoId, bancoId,
            MoedaOriginal: "Usd",
            ValorOferecido: 100_000m,
            TaxaAa: 6.5m,
            IofPct: 0.38m,
            SpreadAa: 0.5m,
            PrazoDias: 180,
            EstruturaAmortizacao: "Bullet",
            PeriodicidadeJuros: "Bullet",
            ExigeNdf: false,
            CustoNdfAa: null,
            GarantiaExigida: "Aval",
            ValorGarantiaBrl: 600_000m,
            GarantiaEhCdbCativo: false,
            RendimentoCdbAa: null);

    [Fact]
    public async Task Handle_PropostaUsdValida_AdicionaPropostaComCetCalculado()
    {
        // Arrange
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoUsd(cotacao.Id, Guid.NewGuid());

        // Act
        PropostaDto resultado = await handler.Handle(cmd, default);

        // Assert — proposta foi adicionada ao agregado
        cotacao.Propostas.Should().HaveCount(1);
        resultado.MoedaOriginal.Should().Be("Usd");
        // CET deve ter sido calculado (não null)
        resultado.CetCalculadoAaPercentual.Should().NotBeNull();
        resultado.CetCalculadoAaPercentual.Should().BeGreaterThan(0m);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_PropostaUsd_CetInclusiOfESpreadNaFormula()
    {
        // Arrange — verifica que CET > taxa nominal bruta (IOF e spread estão inclusos)
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoUsd(cotacao.Id, Guid.NewGuid());

        PropostaDto resultado = await handler.Handle(cmd, default);

        // IOF (0.38%) eleva o CET acima da taxa bruta (6.5 + 0.5 = 7%)
        resultado.CetCalculadoAaPercentual.Should().BeGreaterThan(cmd.TaxaAa + cmd.SpreadAa);
    }

    [Fact]
    public async Task Handle_PropostaCny_UsaCrossRateParaCalcularPtaxEfetiva()
    {
        // Arrange — CNY não é USD nem BRL, exige cross-rate via fxRepo
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        // Cross-rate CNY/USD: 1 CNY = 0.14 USD
        CotacaoFx cnyUsd = CotacaoFx.Criar(
            Moeda.Cny,
            TipoCotacao.PtaxD1,
            new Money(0.13m, Moeda.Usd),
            new Money(0.14m, Moeda.Usd),
            "BACEN",
            TestHelpers.AgentInstant.Minus(NodaTime.Duration.FromHours(2)));

        fxRepo.GetMaisRecenteAsync(Moeda.Cny, TipoCotacao.PtaxD1, cotacao.DataPtaxReferencia, default)
            .Returns(cnyUsd);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = new(
            cotacao.Id, Guid.NewGuid(),
            MoedaOriginal: "Cny",
            ValorOferecido: 700_000m,
            TaxaAa: 4.0m,
            IofPct: 0.38m,
            SpreadAa: 0.5m,
            PrazoDias: 180,
            EstruturaAmortizacao: "Bullet",
            PeriodicidadeJuros: "Bullet",
            ExigeNdf: false,
            CustoNdfAa: null,
            GarantiaExigida: "Aval",
            ValorGarantiaBrl: 500_000m,
            GarantiaEhCdbCativo: false,
            RendimentoCdbAa: null);

        // Act
        PropostaDto resultado = await handler.Handle(cmd, default);

        // Assert — cross-rate foi consultado
        await fxRepo.Received(1).GetMaisRecenteAsync(Moeda.Cny, TipoCotacao.PtaxD1, cotacao.DataPtaxReferencia, default);
        resultado.CetCalculadoAaPercentual.Should().NotBeNull();
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_CrossRateIndisponivel_LancaInvalidOperationException()
    {
        // Arrange
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        fxRepo.GetMaisRecenteAsync(Moeda.Cny, TipoCotacao.PtaxD1, Arg.Any<NodaTime.LocalDate>(), default)
            .Returns((CotacaoFx?)null);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = new(
            cotacao.Id, Guid.NewGuid(),
            MoedaOriginal: "Cny",
            ValorOferecido: 100_000m,
            TaxaAa: 4.0m,
            IofPct: 0.38m,
            SpreadAa: 0.5m,
            PrazoDias: 180,
            EstruturaAmortizacao: "Bullet",
            PeriodicidadeJuros: "Bullet",
            ExigeNdf: false,
            CustoNdfAa: null,
            GarantiaExigida: "Aval",
            ValorGarantiaBrl: 300_000m,
            GarantiaEhCdbCativo: false,
            RendimentoCdbAa: null);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cross-rate*");
    }
}
