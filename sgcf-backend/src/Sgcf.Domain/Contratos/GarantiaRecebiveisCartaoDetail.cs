using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de garantia do tipo Recebíveis de Cartão — extensão 1:1 com <see cref="Garantia"/>.
/// </summary>
public sealed class GarantiaRecebiveisCartaoDetail : Entity
{
    public Guid GarantiaId { get; private set; }

    /// <summary>Operadora do cartão (ex: Cielo, Stone, Rede).</summary>
    public string OperadoraCartao { get; private set; } = default!;

    /// <summary>Tipo de recebível (ex: VISTA, PARCELADO_SEM_JUROS).</summary>
    public string TipoRecebivel { get; private set; } = default!;

    /// <summary>Percentual do faturamento comprometido, armazenado como fração (0..1).</summary>
    internal decimal PercentualFaturamentoComprometidoDecimal { get; private set; }

    /// <summary>Percentual do faturamento comprometido com esta garantia.</summary>
    public Percentual PercentualFaturamentoComprometido =>
        Percentual.DeFracao(PercentualFaturamentoComprometidoDecimal);

    internal decimal? ValorMedioMensalReferenciaDecimal { get; private set; }

    /// <summary>Valor médio mensal de referência em BRL. Null quando não informado.</summary>
    public Money? ValorMedioMensalReferencia =>
        ValorMedioMensalReferenciaDecimal.HasValue
            ? new Money(ValorMedioMensalReferenciaDecimal.Value, Moeda.Brl)
            : null;

    public int PrazoRecebimentoDias { get; private set; }

    /// <summary>URL do termo de cessão assinado. Null quando não disponível.</summary>
    public string? TermoCessaoUrl { get; private set; }

    private GarantiaRecebiveisCartaoDetail() { }

    /// <summary>
    /// Cria um novo detalhe de Recebíveis de Cartão.
    /// <paramref name="percentualFaturamentoPct"/> é fornecido como valor humano (ex: 30.0 para 30%).
    /// </summary>
    public static GarantiaRecebiveisCartaoDetail Criar(
        Guid garantiaId,
        string operadoraCartao,
        string tipoRecebivel,
        decimal percentualFaturamentoPct,
        decimal? valorMedioMensalBrl,
        int prazoRecebimentoDias,
        string? termoCessaoUrl)
    {
        if (string.IsNullOrWhiteSpace(operadoraCartao))
        {
            throw new ArgumentException("OperadoraCartao não pode ser vazio.", nameof(operadoraCartao));
        }

        if (string.IsNullOrWhiteSpace(tipoRecebivel))
        {
            throw new ArgumentException("TipoRecebivel não pode ser vazio.", nameof(tipoRecebivel));
        }

        if (percentualFaturamentoPct <= 0m || percentualFaturamentoPct > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentualFaturamentoPct), "PercentualFaturamento deve estar entre 0 e 100 (exclusivo).");
        }

        if (prazoRecebimentoDias <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prazoRecebimentoDias), "PrazoRecebimentoDias deve ser maior que zero.");
        }

        return new GarantiaRecebiveisCartaoDetail
        {
            GarantiaId = garantiaId,
            OperadoraCartao = operadoraCartao,
            TipoRecebivel = tipoRecebivel,
            PercentualFaturamentoComprometidoDecimal = percentualFaturamentoPct / 100m,
            ValorMedioMensalReferenciaDecimal = valorMedioMensalBrl,
            PrazoRecebimentoDias = prazoRecebimentoDias,
            TermoCessaoUrl = termoCessaoUrl
        };
    }
}
