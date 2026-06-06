using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Cambio;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Exceptions;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// PTAX multimoeda na criação de cotação — SPEC S40 §6.
/// Resolve PTAX por moedaAlvo; ptaxUsada canônico; ptaxUsadaUsdBrl só para USD; erro tipado quando ausente.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CriarCotacaoMoedaAlvoTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 16, 9, 0);
    private static readonly LocalDate DataAbertura = new(2026, 5, 16);

    private static IClock Clock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static CotacaoFx Fx(Moeda moeda, decimal venda) =>
        CotacaoFx.Criar(
            moeda,
            TipoCotacao.PtaxD0,
            new Money(venda - 0.05m, Moeda.Brl),
            new Money(venda, Moeda.Brl),
            "BACEN",
            Agora.Minus(Duration.FromHours(10)));

    private static (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) Mocks()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.GerarProximoCodigoInternoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns("COT-2026-00001");
        IResolveTipoCotacaoService resolver = Substitute.For<IResolveTipoCotacaoService>();
        return (repo, resolver);
    }

    [Fact]
    public async Task Lei4131_com_moeda_eur_resolve_ptax_eur_e_zera_alias_usd()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        resolver.ResolverFxAsync(Moeda.Eur, TipoCotacao.PtaxD1, DataAbertura, Arg.Any<CancellationToken>())
            .Returns(Fx(Moeda.Eur, 6.10m));

        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());
        CriarCotacaoCommand cmd = new(
            Modalidade: "Lei4131",
            ValorAlvoBrl: 5_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoValor: 60,
            PrazoMaximoUnidade: "Meses",
            MoedaAlvo: "Eur");

        CotacaoDto result = await handler.Handle(cmd, default);

        result.MoedaAlvo.Should().Be("Eur");
        result.PtaxUsada.Should().Be(6.10m);
        result.PtaxUsadaUsdBrl.Should().BeNull();
        result.PrazoMaximoDias.Should().Be(1800);
    }

    [Fact]
    public async Task Finimp_com_moeda_usd_espelha_alias_legado()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        resolver.ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataAbertura, Arg.Any<CancellationToken>())
            .Returns(Fx(Moeda.Usd, 5.20m));

        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());
        CriarCotacaoCommand cmd = new(
            Modalidade: "Finimp",
            ValorAlvoBrl: 1_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoDias: 180,
            MoedaAlvo: "Usd");

        CotacaoDto result = await handler.Handle(cmd, default);

        result.PtaxUsada.Should().Be(5.20m);
        result.PtaxUsadaUsdBrl.Should().Be(5.20m);
    }

    [Fact]
    public async Task Finimp_sem_moeda_usa_default_usd()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        resolver.ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataAbertura, Arg.Any<CancellationToken>())
            .Returns(Fx(Moeda.Usd, 5.20m));

        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());
        CriarCotacaoCommand cmd = new(
            Modalidade: "Finimp",
            ValorAlvoBrl: 1_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoDias: 180);

        CotacaoDto result = await handler.Handle(cmd, default);

        result.MoedaAlvo.Should().Be("Usd");
        result.PtaxUsada.Should().Be(5.20m);
    }

    [Fact]
    public async Task Ptax_ausente_lanca_excecao_tipada_com_moeda_e_data()
    {
        (ICotacaoRepository repo, IResolveTipoCotacaoService resolver) = Mocks();
        // resolver não mockado para Jpy → retorna null.

        CriarCotacaoCommandHandler handler = new(repo, resolver, Clock());
        CriarCotacaoCommand cmd = new(
            Modalidade: "Lei4131",
            ValorAlvoBrl: 1_000_000m,
            DataAbertura: new DateOnly(2026, 5, 16),
            PrazoMaximoValor: 24,
            PrazoMaximoUnidade: "Meses",
            MoedaAlvo: "Jpy");

        Func<Task> act = () => handler.Handle(cmd, default);

        PtaxIndisponivelException ex = (await act.Should().ThrowAsync<PtaxIndisponivelException>()).Which;
        ex.MoedaAlvo.Should().Be("Jpy");
        ex.DataReferencia.Should().Be(new DateOnly(2026, 5, 15));
    }
}
