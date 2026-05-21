namespace Sgcf.Application.Hedge;

/// <summary>
/// Representação de leitura de um snapshot diário de MtM.
///
/// <para><see cref="DataReferencia"/>: formato yyyy-MM-dd.</para>
/// <para><see cref="Posicao"/>: computado em tempo de mapeamento a partir do sinal do payoff —
/// "RECEBER" quando positivo, "PAGAR" quando negativo, "NEUTRO" quando zero.</para>
/// </summary>
public sealed record HistoricoMtmDiarioDto(
    string DataReferencia,
    decimal PayoffBrl,
    string Posicao,
    decimal SpotUtilizado,
    string TipoCotacao);
