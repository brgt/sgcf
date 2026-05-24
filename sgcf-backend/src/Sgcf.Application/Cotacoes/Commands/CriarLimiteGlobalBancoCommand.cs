using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Cria um limite global (guarda-chuva) para o banco informado.
/// SPEC §7.1 — LG-05, LG-13.
/// </summary>
public sealed record CriarLimiteGlobalBancoCommand(
    Guid BancoId,
    decimal ValorLimiteBrl,
    DateOnly DataVigenciaInicio,
    DateOnly? DataVigenciaFim,
    string? Observacoes) : IRequest<LimiteGlobalBancoDto>;

public sealed class CriarLimiteGlobalBancoCommandValidator : AbstractValidator<CriarLimiteGlobalBancoCommand>
{
    public CriarLimiteGlobalBancoCommandValidator()
    {
        RuleFor(c => c.BancoId).NotEmpty();

        RuleFor(c => c.ValorLimiteBrl)
            .GreaterThan(0m)
            .WithMessage("ValorLimiteBrl deve ser maior que zero.");
    }
}

public sealed class CriarLimiteGlobalBancoCommandHandler(
    ILimiteGlobalBancoRepository repo,
    IBancoRepository bancoRepo,
    IConsultaSaldoBanco saldo,
    ITenantContext tenantContext,
    IClock clock) : IRequestHandler<CriarLimiteGlobalBancoCommand, LimiteGlobalBancoDto>
{
    public async Task<LimiteGlobalBancoDto> Handle(CriarLimiteGlobalBancoCommand cmd, CancellationToken cancellationToken)
    {
        // Step 1: verify banco exists.
        var banco = await bancoRepo.GetByIdAsync(cmd.BancoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Banco {cmd.BancoId} não encontrado.");

        LocalDate inicio = new(cmd.DataVigenciaInicio.Year, cmd.DataVigenciaInicio.Month, cmd.DataVigenciaInicio.Day);
        LocalDate? fim = cmd.DataVigenciaFim.HasValue
            ? new LocalDate(cmd.DataVigenciaFim.Value.Year, cmd.DataVigenciaFim.Value.Month, cmd.DataVigenciaFim.Value.Day)
            : null;

        // Step 2: LG-05 — reject overlapping vigência for the same banco.
        LimiteGlobalBanco? conflito = await repo.FindOverlappingAsync(cmd.BancoId, inicio, fim, ct: cancellationToken);
        if (conflito is not null)
        {
            throw new InvalidOperationException(
                "Já existe LimiteGlobalBanco com vigência sobreposta para este banco.");
        }

        Guid tenantId = tenantContext.TenantId;

        // Step 3: LG-13 — if banco has per-modality limits, the sum of those limits
        //         must not exceed the proposed global limit.
        bool perModality = await saldo.BancoEmRegimePerModalityAsync(cmd.BancoId, tenantId, cancellationToken);
        if (perModality)
        {
            Money somaLimites = await saldo.CalcularSomaLimitesModalidadesAsync(
                cmd.BancoId, tenantId, excluirLimiteBancoId: null, cancellationToken);

            if (somaLimites.Valor > cmd.ValorLimiteBrl)
            {
                throw new InvalidOperationException(
                    $"Soma dos limites por modalidade (BRL {somaLimites.Valor:F2}) excede o novo limite " +
                    $"global proposto (BRL {cmd.ValorLimiteBrl:F2}).");
            }
        }

        // Step 4: create the aggregate.
        LimiteGlobalBanco limite = LimiteGlobalBanco.Criar(
            cmd.BancoId,
            new Money(cmd.ValorLimiteBrl, Moeda.Brl),
            inicio,
            clock,
            fim,
            cmd.Observacoes);

        // Step 5: TenantSaveInterceptor sets TenantId on SaveChanges; no manual assignment needed.
        repo.Add(limite);
        await repo.SaveChangesAsync(cancellationToken);

        return LimiteGlobalBancoDto.From(limite);
    }
}
