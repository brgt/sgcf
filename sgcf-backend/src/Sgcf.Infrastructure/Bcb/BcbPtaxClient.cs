using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Infrastructure.Bcb;

internal sealed record BcbBoletim(
    [property: JsonPropertyName("cotacaoCompra")] decimal CotacaoCompra,
    [property: JsonPropertyName("cotacaoVenda")] decimal CotacaoVenda,
    [property: JsonPropertyName("dataHoraCotacao")] string DataHoraCotacao,
    [property: JsonPropertyName("tipoBoletim")] string TipoBoletim);

internal sealed record BcbOlinhaResponse(
    [property: JsonPropertyName("value")] IReadOnlyList<BcbBoletim> Value);

public sealed partial class BcbPtaxClient(HttpClient httpClient, ILogger<BcbPtaxClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "BCB PTAX boletins obtidos: {Moeda} total={Total}.")]
    private static partial void LogBoletinsObtidos(ILogger logger, string moeda, int total);

    internal async Task<IReadOnlyList<BcbBoletim>> GetBoletinsAsync(
        Moeda moeda,
        LocalDate data,
        CancellationToken cancellationToken)
    {
        string url = BuildUrl(moeda, data);
        HttpResponseMessage resposta = await httpClient.GetAsync(url, cancellationToken);
        resposta.EnsureSuccessStatusCode();
        string json = await resposta.Content.ReadAsStringAsync(cancellationToken);
        BcbOlinhaResponse? response = JsonSerializer.Deserialize<BcbOlinhaResponse>(json, JsonOptions);
        IReadOnlyList<BcbBoletim> boletins = response?.Value ?? (IReadOnlyList<BcbBoletim>)Array.Empty<BcbBoletim>();
        string codigoMoeda = GetCurrencyCode(moeda);
        LogBoletinsObtidos(logger, codigoMoeda, boletins.Count);
        return boletins;
    }

    internal static string BuildUrl(Moeda moeda, LocalDate data)
    {
        string codigoMoeda = GetCurrencyCode(moeda);
        string dataFormatada = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:00}-{1:00}-{2:0000}",
            data.Month,
            data.Day,
            data.Year);

        return moeda == Moeda.Usd
            ? $"https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/CotacaoDolarDia(dataCotacao=@dataCotacao)?@dataCotacao='{dataFormatada}'&$top=100&$format=json&$select=cotacaoCompra,cotacaoVenda,dataHoraCotacao,tipoBoletim"
            : $"https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/CotacaoMoedaDia(moeda=@moeda,dataCotacao=@dataCotacao)?@moeda='{codigoMoeda}'&@dataCotacao='{dataFormatada}'&$top=100&$format=json&$select=cotacaoCompra,cotacaoVenda,dataHoraCotacao,tipoBoletim";
    }

    private static string GetCurrencyCode(Moeda moeda) => moeda switch
    {
        Moeda.Usd => "USD",
        Moeda.Eur => "EUR",
        Moeda.Jpy => "JPY",
        Moeda.Cny => "CNY",
        _ => throw new ArgumentOutOfRangeException(nameof(moeda), moeda, "Moeda não suportada pela BCB.")
    };
}
