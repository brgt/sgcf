using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Remove o item do tipo informado da revisão vigente de garantias exigidas do limite.
/// Fecha a revisão vigente e abre nova com (itens da anterior − tipo informado).
/// SPEC §5.1 — <c>DELETE /api/v1/limites-banco/{id}/garantias-exigidas?tipo=X</c>.
/// </summary>
public sealed record RemoverGarantiaExigidaPorTipoCommand(
    Guid LimiteId,
    string Tipo) : IRequest;

public sealed class RemoverGarantiaExigidaPorTipoCommandValidator
    : AbstractValidator<RemoverGarantiaExigidaPorTipoCommand>
{
    public RemoverGarantiaExigidaPorTipoCommandValidator()
    {
        RuleFor(c => c.LimiteId).NotEmpty();

        RuleFor(c => c.Tipo)
            .NotEmpty()
            .Must(v => Enum.TryParse<TipoGarantia>(v, ignoreCase: true, out _))
            .WithMessage(c =>
                $"Tipo de garantia inválido: '{c.Tipo}'. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoGarantia>())}.");
    }
}

public sealed class RemoverGarantiaExigidaPorTipoCommandHandler(
    ILimiteBancoRepository repo,
    IClock clock)
    : IRequestHandler<RemoverGarantiaExigidaPorTipoCommand>
{
    public async Task Handle(
        RemoverGarantiaExigidaPorTipoCommand cmd,
        CancellationToken cancellationToken)
    {
        LimiteBanco limite = await repo.GetByIdTrackingAsync(cmd.LimiteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Limite '{cmd.LimiteId}' não encontrado.");

        TipoGarantia tipo = Enum.Parse<TipoGarantia>(cmd.Tipo, ignoreCase: true);

        // Domain throws InvalidOperationException se:
        // - não há revisão vigente;
        // - o tipo não está na revisão vigente.
        limite.RemoverGarantiaExigidaPorTipo(tipo, clock);

        repo.Update(limite);
        await repo.SaveChangesAsync(cancellationToken);
    }
}
