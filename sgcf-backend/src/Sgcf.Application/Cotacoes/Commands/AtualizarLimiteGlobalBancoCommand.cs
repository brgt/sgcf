using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Atualiza um limite global com semântica PATCH: campos null preservam o valor atual.
/// SPEC §7.2 — LG-06, LG-09.
/// </summary>
public sealed record AtualizarLimiteGlobalBancoCommand(
    Guid Id,
    decimal? ValorLimiteBrl,
    DateOnly? DataVigenciaInicio,
    DateOnly? DataVigenciaFim,
    string? Observacoes) : IRequest<LimiteGlobalBancoDto>;

public sealed class AtualizarLimiteGlobalBancoCommandValidator : AbstractValidator<AtualizarLimiteGlobalBancoCommand>
{
    public AtualizarLimiteGlobalBancoCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.ValorLimiteBrl)
            .GreaterThan(0m)
            .WithMessage("ValorLimiteBrl deve ser maior que zero.")
            .When(c => c.ValorLimiteBrl.HasValue);
    }
}

public sealed class AtualizarLimiteGlobalBancoCommandHandler(
    ILimiteGlobalBancoRepository repo,
    IConsultaSaldoBanco saldo,
    ITenantContext tenantContext,
    IClock clock) : IRequestHandler<AtualizarLimiteGlobalBancoCommand, LimiteGlobalBancoDto>
{
    public async Task<LimiteGlobalBancoDto> Handle(AtualizarLimiteGlobalBancoCommand cmd, CancellationToken cancellationToken)
    {
        LimiteGlobalBanco limite = await repo.GetByIdTrackingAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"LimiteGlobalBanco {cmd.Id} não encontrado.");

        Money? novoLimiteBrl = null;
        Money? saldoDevedor = null;

        if (cmd.ValorLimiteBrl.HasValue)
        {
            Guid tenantId = tenantContext.TenantId;
            Guid bancoId = limite.BancoId;

            // LG-06: determine current utilization to prevent reducing below the outstanding balance.
            // The utilization source depends on which regime the banco operates under.
            bool perModality = await saldo.BancoEmRegimePerModalityAsync(bancoId, tenantId, cancellationToken);

            saldoDevedor = perModality
                ? await saldo.CalcularUtilizadoAgregadoModalidadesAsync(bancoId, tenantId, cancellationToken)
                : await saldo.CalcularSaldoDevedorBancoAsync(bancoId, tenantId, cancellationToken);

            // LG-09: in per-modality regime, the sum of modality limits must not exceed the new global.
            if (perModality)
            {
                Money somaLimites = await saldo.CalcularSomaLimitesModalidadesAsync(
                    bancoId, tenantId, excluirLimiteBancoId: null, cancellationToken);

                if (somaLimites.Valor > cmd.ValorLimiteBrl.Value)
                {
                    throw new InvalidOperationException(
                        $"Soma dos limites por modalidade (BRL {somaLimites.Valor:F2}) excede o novo limite " +
                        $"global proposto (BRL {cmd.ValorLimiteBrl.Value:F2}).");
                }
            }

            novoLimiteBrl = new Money(cmd.ValorLimiteBrl.Value, Moeda.Brl);
        }

        LocalDate? novaDataInicio = cmd.DataVigenciaInicio.HasValue
            ? new LocalDate(cmd.DataVigenciaInicio.Value.Year, cmd.DataVigenciaInicio.Value.Month, cmd.DataVigenciaInicio.Value.Day)
            : null;

        LocalDate? novaDataFim = cmd.DataVigenciaFim.HasValue
            ? new LocalDate(cmd.DataVigenciaFim.Value.Year, cmd.DataVigenciaFim.Value.Month, cmd.DataVigenciaFim.Value.Day)
            : null;

        // Domain enforces LG-06 (saldo ≤ novo limite) and LG-03 (date coherence).
        limite.Atualizar(clock, novoLimiteBrl, novaDataInicio, novaDataFim, cmd.Observacoes, saldoDevedor);

        await repo.SaveChangesAsync(cancellationToken);

        return LimiteGlobalBancoDto.From(limite);
    }
}
