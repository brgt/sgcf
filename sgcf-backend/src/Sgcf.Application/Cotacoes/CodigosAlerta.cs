namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Registro central dos códigos estáveis de alerta de cotação (contrato com o front-end).
/// Toda emissão de alerta deve referenciar uma destas constantes — SPEC S40 §4.6.
/// </summary>
public static class CodigosAlerta
{
    /// <summary>Tenor {valor, unidade} divergente de prazoMaximoDias enviado; o par prevaleceu.</summary>
    public const string PrazoRecalculado = "prazo-recalculado";

    /// <summary>Prazo acima da faixa típica esperada para a modalidade (não bloqueante).</summary>
    public const string PrazoForaDaFaixaEsperada = "prazo-fora-da-faixa-esperada";

    /// <summary>indexadorBase.tipo informado sem o campo numérico coerente.</summary>
    public const string IndexadorIncoerente = "indexador-incoerente";

    /// <summary>carenciaMeses enviado a modalidade não aplicável (ignorado).</summary>
    public const string CarenciaIgnorada = "carencia-ignorada";

    /// <summary>moedaAlvo divergente enviada em Refinimp; herdada do contrato mãe.</summary>
    public const string MoedaHerdadaDoContratoMae = "moeda-herdada-do-contrato-mae";
}
