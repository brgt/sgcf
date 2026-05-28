namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Resposta de todos os PATCH /limites-banco/{id}.
/// Inclui o DTO atualizado e possíveis avisos operacionais não bloqueantes.
/// RV-01 — SPEC de reavaliação de crédito.
/// </summary>
public sealed record AtualizarLimiteBancoResponse(
    LimiteBancoDto Limite,
    IReadOnlyList<string> Avisos);
