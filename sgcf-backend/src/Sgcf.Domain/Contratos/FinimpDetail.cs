using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

public sealed class FinimpDetail : Entity
{
    public Guid ContratoId { get; private set; }
    public string? RofNumero { get; private set; }
    public LocalDate? RofDataEmissao { get; private set; }
    public string? ExportadorNome { get; private set; }
    public string? ExportadorPais { get; private set; }
    public string? ProdutoImportado { get; private set; }
    public string? FaturaReferencia { get; private set; }
    public string? Incoterm { get; private set; }
    public decimal? BreakFundingFeePercentual { get; private set; }
    public bool TemMarketFlex { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private FinimpDetail() { }

    public static FinimpDetail Criar(
        Guid contratoId,
        string? rofNumero,
        LocalDate? rofDataEmissao,
        string? exportadorNome,
        string? exportadorPais,
        string? produtoImportado,
        string? faturaReferencia,
        string? incoterm,
        decimal? breakFundingFeePercentual,
        bool temMarketFlex,
        IClock clock)
    {
        Instant now = clock.GetCurrentInstant();
        return new FinimpDetail
        {
            ContratoId = contratoId,
            RofNumero = rofNumero,
            RofDataEmissao = rofDataEmissao,
            ExportadorNome = exportadorNome,
            ExportadorPais = exportadorPais,
            ProdutoImportado = produtoImportado,
            FaturaReferencia = faturaReferencia,
            Incoterm = incoterm,
            BreakFundingFeePercentual = breakFundingFeePercentual.HasValue
                ? breakFundingFeePercentual.Value / 100m
                : (decimal?)null,
            TemMarketFlex = temMarketFlex,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
