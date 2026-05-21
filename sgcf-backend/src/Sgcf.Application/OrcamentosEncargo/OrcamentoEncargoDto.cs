namespace Sgcf.Application.OrcamentosEncargo;

/// <summary>
/// DTO de saída para orçamento de encargo financeiro.
/// </summary>
public sealed record OrcamentoEncargoDto(
    Guid Id,
    int Ano,
    int Mes,
    string TipoEncargo,
    decimal ValorOrcadoBrl,
    Guid? BancoId,
    Guid? ContratoId,
    string? Observacao);
