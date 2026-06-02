using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Infrastructure.Cambio;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// RF-11 — regressão de off-by-one consolidada. Após a correção, Perfil A (CriarCotacao)
/// e Perfil B (Painel/Tesouraria) passam ambos a <b>data de referência R</b> ao resolver
/// (sem pré-subtrair) e dependem da <b>mesma</b> regra única de tradução
/// <c>PtaxD1 → PtaxD0(R-1)</c>. Este teste fixa essa regra: para a referência R, o resolver
/// consulta <c>PtaxD0</c> em R-1 (nunca R nem R-2) e retorna o fechamento correspondente,
/// garantindo que os dois perfis resolvam o mesmo fechamento.
/// Os off-by-one específicos de cada chamador são cobertos por
/// <c>CriarCotacaoCommandHandlerTests</c> (unit) e <c>CriarCotacaoPtaxD0Tests</c> (integração).
/// </summary>
[Trait("Category", "Domain")]
public sealed class CotacaoResolverParidadePerfilTests
{
    private static readonly DateTimeZone FusoBrasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    // Referência comum aos dois perfis.
    private static readonly LocalDate R = new(2026, 5, 16);
    private static readonly LocalDate FechamentoEsperado = new(2026, 5, 15); // R-1

    [Fact]
    public async Task PerfilA_e_PerfilB_ResolvemMesmoFechamentoPtaxD0_DeRMenos1()
    {
        // O repositório só responde para PtaxD0 em R-1 (2026-05-15). Qualquer outra data
        // (ex.: R-2 por off-by-one) retorna null e faria o teste falhar.
        CotacaoFx fechamento = CotacaoFx.Criar(
            Moeda.Usd,
            TipoCotacao.PtaxD0,
            new Money(5.15m, Moeda.Brl),
            new Money(5.20m, Moeda.Brl),
            "BCB_OLINDA",
            Instant.FromUtc(2026, 5, 15, 20, 0));

        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        cotacaoRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD0, FechamentoEsperado, Arg.Any<CancellationToken>())
            .Returns(fechamento);

        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(R.AtStartOfDayInZone(FusoBrasilia).ToInstant() + Duration.FromHours(12));

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        // Perfil A: CriarCotacao passa dataAbertura = R.
        CotacaoFx? perfilA = await sut.ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD1, R, CancellationToken.None);

        // Perfil B: Painel/Tesouraria passam hoje = R (mesma data corrente).
        CotacaoFx? perfilB = await sut.ResolverFxAsync(Moeda.Usd, TipoCotacao.PtaxD1, R, CancellationToken.None);

        perfilA.Should().NotBeNull("o fechamento PtaxD0 de R-1 deve ser resolvido (não R-2, não null)");
        perfilB.Should().NotBeNull();
        perfilA!.Momento.Should().Be(perfilB!.Momento, "ambos os perfis resolvem o mesmo fechamento");
        perfilA.Momento.Should().Be(fechamento.Momento);

        // Confirma que a consulta foi feita em PtaxD0 na data R-1 (a tradução D-1 do resolver).
        await cotacaoRepo.Received().GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.PtaxD0, FechamentoEsperado, Arg.Any<CancellationToken>());
        await cotacaoRepo.DidNotReceive().GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.PtaxD1, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>());
    }
}
