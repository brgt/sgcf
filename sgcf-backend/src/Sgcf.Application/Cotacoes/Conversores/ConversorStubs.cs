using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Cotacoes.Conversores;

// ConversorRefinimp foi migrado para ConversorRefinimp.cs (Onda 1 — implementação real).
// ConversorNce foi migrado para ConversorNce.cs (Onda 2 — implementação real).
// ConversorLei4131 foi migrado para ConversorLei4131.cs (Onda 4 — implementação real).

/// <summary>
/// Stub do conversor Capital de Giro. Implementação completa entregue na Onda 3.
/// </summary>
public sealed class ConversorCapitalDeGiro : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.CapitalDeGiro;

    /// <inheritdoc/>
    public Task<(Entity, Entity?)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "Conversor da modalidade CapitalDeGiro será entregue na Onda 3. " +
            "Veja docs/specs/cotacoes/modalidades/capital-de-giro.md.");
}

/// <summary>
/// Stub do conversor FGI. Implementação completa entregue na Onda 3.
/// </summary>
public sealed class ConversorFgi : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.Fgi;

    /// <inheritdoc/>
    public Task<(Entity, Entity?)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "Conversor da modalidade Fgi será entregue na Onda 3. " +
            "Veja docs/specs/cotacoes/modalidades/fgi.md.");
}
