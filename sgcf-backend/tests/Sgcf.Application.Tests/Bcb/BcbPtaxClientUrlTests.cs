using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Infrastructure.Bcb;
using Xunit;

namespace Sgcf.Application.Tests.Bcb;

[Trait("Category", "Domain")]
public sealed class BcbPtaxClientUrlTests
{
    // ── USD usa endpoint CotacaoDolarDia ──────────────────────────────────────

    [Fact]
    public void BuildUrl_Usd_UsaEndpointDolarDia()
    {
        LocalDate data = new LocalDate(2026, 5, 11);
        string url = BcbPtaxClient.BuildUrl(Moeda.Usd, data);
        url.Should().Contain("CotacaoDolarDia");
        url.Should().NotContain("CotacaoMoedaDia");
    }

    // ── EUR usa endpoint CotacaoMoedaDia com código EUR ──────────────────────

    [Fact]
    public void BuildUrl_Eur_UsaEndpointMoedaDia()
    {
        LocalDate data = new LocalDate(2026, 5, 11);
        string url = BcbPtaxClient.BuildUrl(Moeda.Eur, data);
        url.Should().Contain("CotacaoMoedaDia");
        url.Should().Contain("'EUR'");
    }

    // ── JPY usa endpoint CotacaoMoedaDia com código JPY ──────────────────────

    [Fact]
    public void BuildUrl_Jpy_UsaEndpointMoedaDia()
    {
        LocalDate data = new LocalDate(2026, 5, 11);
        string url = BcbPtaxClient.BuildUrl(Moeda.Jpy, data);
        url.Should().Contain("'JPY'");
    }

    // ── Data formatada corretamente MM-DD-YYYY ────────────────────────────────

    [Fact]
    public void BuildUrl_DataComDiaMes1Digito_FormatadaCorretamente()
    {
        LocalDate data = new LocalDate(2026, 1, 5); // Jan 5
        string url = BcbPtaxClient.BuildUrl(Moeda.Usd, data);
        url.Should().Contain("'01-05-2026'");
    }

    [Fact]
    public void BuildUrl_DataComDiaMes2Digitos_FormatadaCorretamente()
    {
        LocalDate data = new LocalDate(2026, 12, 31);
        string url = BcbPtaxClient.BuildUrl(Moeda.Usd, data);
        url.Should().Contain("'12-31-2026'");
    }

    // ── URL inclui campos obrigatórios ────────────────────────────────────────

    [Fact]
    public void BuildUrl_SempreInclui_SelectEFormatJson()
    {
        LocalDate data = new LocalDate(2026, 5, 11);
        string url = BcbPtaxClient.BuildUrl(Moeda.Usd, data);
        url.Should().Contain("$format=json");
        url.Should().Contain("cotacaoCompra");
        url.Should().Contain("cotacaoVenda");
        url.Should().Contain("dataHoraCotacao");
        url.Should().Contain("tipoBoletim");
    }
}
