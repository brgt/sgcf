using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Cambio;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Campos de domínio na criação: carência e indexador, com validação suave — SPEC S40 §2.3, §2.4, §4.5.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CriarCotacaoDominioTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 16, 9, 0);

    private static IClock Clock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) Mocks()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.GerarProximoCodigoInternoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns("COT-2026-00001");
        IResolveTipoCotacaoService resolver = Substitute.For<IResolveTipoCotacaoService>();
        return (repo, resolver);
    }

    private static CotacaoFx FxUsd() =>
        CotacaoFx.Criar(Moeda.Usd, TipoCotacao.PtaxD0,
            new Money(5.15m, Moeda.Brl), new Money(5.20m, Moeda.Brl),
            "BACEN", Agora.Minus(Duration.FromHours(10)));

    [Fact]
    public async Task Fgi_armazena_carencia_e_indexador_sem_alertas()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());

        CriarCotacaoCommand cmd = new(
            Modalidade: "Fgi",
            ValorAlvoBrl: 5_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoValor: 48,
            PrazoMaximoUnidade: "Meses",
            CarenciaMeses: 18,
            IndexadorBase: new IndexadorBaseInput(Tipo: "Tlp", SpreadAa: 1.5m));

        CotacaoDto result = await handler.Handle(cmd, default);

        result.CarenciaMeses.Should().Be(18);
        result.IndexadorBase!.Tipo.Should().Be("Tlp");
        result.IndexadorBase.SpreadAa.Should().Be(1.5m);
        result.Alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Carencia_em_modalidade_nao_aplicavel_eh_ignorada_com_alerta()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        resolver.ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD1, new LocalDate(2026, 5, 16), Arg.Any<CancellationToken>())
            .Returns(FxUsd());
        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());

        CriarCotacaoCommand cmd = new(
            Modalidade: "Finimp",
            ValorAlvoBrl: 1_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoDias: 180,
            CarenciaMeses: 12);

        CotacaoDto result = await handler.Handle(cmd, default);

        result.CarenciaMeses.Should().BeNull();
        result.Alertas.Should().ContainSingle(a => a.Codigo == "carencia-ignorada");
    }

    [Fact]
    public async Task Indexador_incoerente_gera_alerta_suave()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());

        CriarCotacaoCommand cmd = new(
            Modalidade: "Fgi",
            ValorAlvoBrl: 5_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoValor: 24,
            PrazoMaximoUnidade: "Meses",
            IndexadorBase: new IndexadorBaseInput(Tipo: "Sofr")); // falta spreadAa

        CotacaoDto result = await handler.Handle(cmd, default);

        result.Alertas.Should().ContainSingle(a => a.Codigo == "indexador-incoerente");
    }

    [Fact]
    public async Task Fgi_armazena_estruturantes()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());

        CriarCotacaoCommand cmd = new(
            Modalidade: "Fgi",
            ValorAlvoBrl: 5_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoValor: 60,
            PrazoMaximoUnidade: "Meses",
            PercentualCoberturaFgi: 80m,
            FinalidadeBndes: "Investimento",
            BancoRepassadorPretendido: "BancoDoBrasil");

        CotacaoDto result = await handler.Handle(cmd, default);

        result.PercentualCoberturaFgi.Should().Be(80m);
        result.FinalidadeBndes.Should().Be("Investimento");
        result.BancoRepassadorPretendido.Should().Be("BancoDoBrasil");
    }

    [Fact]
    public async Task Estruturantes_em_modalidade_nao_fgi_sao_ignorados()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());

        CriarCotacaoCommand cmd = new(
            Modalidade: "Nce",
            ValorAlvoBrl: 5_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoValor: 12,
            PrazoMaximoUnidade: "Meses",
            PercentualCoberturaFgi: 80m,
            FinalidadeBndes: "Investimento");

        CotacaoDto result = await handler.Handle(cmd, default);

        result.PercentualCoberturaFgi.Should().BeNull();
        result.FinalidadeBndes.Should().BeNull();
    }
}
