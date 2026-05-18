using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Cotacoes.Conversores;

// ConversorRefinimp foi migrado para ConversorRefinimp.cs (Onda 1 — implementação real).

/// <summary>
/// Stub do conversor Lei 4131. Implementação completa entregue na Onda 4.
/// </summary>
public sealed class ConversorLei4131 : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.Lei4131;

    /// <inheritdoc/>
    public Task<(Entity, Entity?)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "Conversor da modalidade Lei4131 será entregue na Onda 4. " +
            "Veja docs/specs/cotacoes/modalidades/lei4131.md.");
}

/// <summary>
/// Stub do conversor NCE. Implementação completa entregue na Onda 2.
/// </summary>
public sealed class ConversorNce : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.Nce;

    /// <inheritdoc/>
    public Task<(Entity, Entity?)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "Conversor da modalidade Nce será entregue na Onda 2. " +
            "Veja docs/specs/cotacoes/modalidades/nce.md.");
}

/// <summary>
/// Stub do conversor Balcão Caixa. Implementação completa entregue na Onda 3.
/// </summary>
public sealed class ConversorBalcaoCaixa : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.BalcaoCaixa;

    /// <inheritdoc/>
    public Task<(Entity, Entity?)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "Conversor da modalidade BalcaoCaixa será entregue na Onda 3. " +
            "Veja docs/specs/cotacoes/modalidades/balcao-caixa.md.");
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
