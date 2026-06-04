using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Bancos.Commands;

/// <summary>
/// Define o regime de limite de um banco (PerModalidade | GlobalPuro).
/// SPEC_REGIME_LIMITE_EXPLICITO §4.2 — REG-02 / REG-04.
/// </summary>
public sealed record DefinirRegimeLimiteBancoCommand(
    Guid BancoId,
    string RegimeLimite) : IRequest<BancoDto>;

public sealed class DefinirRegimeLimiteBancoCommandValidator : AbstractValidator<DefinirRegimeLimiteBancoCommand>
{
    public DefinirRegimeLimiteBancoCommandValidator()
    {
        RuleFor(c => c.BancoId).NotEmpty();

        RuleFor(c => c.RegimeLimite)
            .NotEmpty()
            .Must(v => Enum.TryParse<RegimeLimiteBanco>(v, ignoreCase: true, out _))
            .WithMessage($"RegimeLimite deve ser um dos valores: {string.Join(", ", Enum.GetNames<RegimeLimiteBanco>())}.");
    }
}

public sealed class DefinirRegimeLimiteBancoCommandHandler(
    IBancoRepository repo,
    IConsultaSaldoBanco saldo,
    ITenantContext tenantContext,
    IClock clock) : IRequestHandler<DefinirRegimeLimiteBancoCommand, BancoDto>
{
    public async Task<BancoDto> Handle(DefinirRegimeLimiteBancoCommand cmd, CancellationToken cancellationToken)
    {
        Banco banco = await repo.GetByIdAsync(cmd.BancoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Banco '{cmd.BancoId}' não encontrado.");

        RegimeLimiteBanco regime = Enum.Parse<RegimeLimiteBanco>(cmd.RegimeLimite, ignoreCase: true);

        // REG-02: não permitir migrar para GlobalPuro enquanto existir LimiteBanco por modalidade ativo.
        // REG-04: migrar para PerModalidade é sempre permitido (não há checagem).
        if (regime == RegimeLimiteBanco.GlobalPuro)
        {
            var somaLimitesModalidades = await saldo.CalcularSomaLimitesModalidadesAsync(
                banco.Id, tenantContext.TenantId, excluirLimiteBancoId: null, cancellationToken);

            if (somaLimitesModalidades.Valor > 0m)
            {
                throw new InvalidOperationException(
                    $"Não é possível mudar o banco '{banco.Apelido}' para regime global: existem limites por modalidade ativos. " +
                    "Encerre-os primeiro. [REG-02]");
            }
        }

        banco.DefinirRegimeLimite(regime, clock);
        await repo.SaveChangesAsync(cancellationToken);
        repo.InvalidarCache(banco.Id);

        return BancoDto.From(banco);
    }
}
