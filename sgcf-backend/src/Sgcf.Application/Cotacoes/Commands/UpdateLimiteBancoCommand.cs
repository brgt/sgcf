using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Atualiza limite operacional com semântica PATCH. SPEC §6.1.
/// NovoValorLimiteBrl null = preservar valor atual.
/// GarantiasExigidas null = preservar garantias atuais; lista vazia = remover todas; populada = substituir todas.
/// </summary>
public sealed record UpdateLimiteBancoCommand(
    Guid LimiteId,
    decimal? NovoValorLimiteBrl = null,
    IReadOnlyList<CriarGarantiaExigidaLimiteRequest>? GarantiasExigidas = null) : IRequest<LimiteBancoDto>;

public sealed class UpdateLimiteBancoCommandValidator : AbstractValidator<UpdateLimiteBancoCommand>
{
    public UpdateLimiteBancoCommandValidator()
    {
        RuleFor(c => c.LimiteId).NotEmpty();

        RuleFor(c => c.NovoValorLimiteBrl)
            .GreaterThan(0m)
            .WithMessage("NovoValorLimiteBrl deve ser maior que zero.")
            .When(c => c.NovoValorLimiteBrl.HasValue);

        // Validate enum names only — XOR delegated to domain.
        RuleForEach(c => c.GarantiasExigidas)
            .ChildRules(g =>
                g.RuleFor(r => r.Tipo)
                 .NotEmpty()
                 .Must(v => Enum.TryParse<TipoGarantia>(v, ignoreCase: true, out _))
                 .WithMessage(r => $"Tipo de garantia inválido: '{r.Tipo}'. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoGarantia>())}."))
            .When(c => c.GarantiasExigidas is not null);
    }
}

public sealed class UpdateLimiteBancoCommandHandler(ILimiteBancoRepository repo, IClock clock)
    : IRequestHandler<UpdateLimiteBancoCommand, LimiteBancoDto>
{
    public async Task<LimiteBancoDto> Handle(UpdateLimiteBancoCommand cmd, CancellationToken cancellationToken)
    {
        // GetByIdAsync already eager-loads GarantiasExigidas + Historico (see repository).
        // AsNoTracking is used there, so we need tracking for the update path.
        LimiteBanco limite = await repo.GetByIdTrackingAsync(cmd.LimiteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Limite '{cmd.LimiteId}' não encontrado.");

        if (cmd.NovoValorLimiteBrl.HasValue)
        {
            Money novoValor = new(cmd.NovoValorLimiteBrl.Value, Moeda.Brl);
            limite.Atualizar(clock, novoLimiteBrl: novoValor);
        }

        if (cmd.GarantiasExigidas is not null)
        {
            IEnumerable<GarantiaExigidaLimiteSpec> specs = cmd.GarantiasExigidas.Select(r => r.ParaSpec());
            limite.SubstituirGarantiasExigidas(specs, clock);
        }

        repo.Update(limite);
        await repo.SaveChangesAsync(cancellationToken);

        return LimiteBancoDto.From(limite);
    }
}
