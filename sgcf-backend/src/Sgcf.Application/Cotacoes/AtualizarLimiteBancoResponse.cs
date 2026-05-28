namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Resposta do PATCH /limites-banco/{id} quando <c>NovaDataVigenciaFim</c> é informado.
/// Inclui o DTO atualizado e possíveis avisos operacionais.
/// RV-01 — SPEC de reavaliação de crédito.
/// </summary>
public sealed record AtualizarLimiteBancoResponse(
    LimiteBancoDto Limite,
    IReadOnlyList<string> Avisos);
