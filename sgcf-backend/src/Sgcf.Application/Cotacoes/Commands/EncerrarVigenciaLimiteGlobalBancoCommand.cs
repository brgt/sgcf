using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Encerra a vigência de um limite global definindo <see cref="LimiteGlobalBanco.DataVigenciaFim"/>.
/// As invariantes LG-08 (vigência já encerrada, data anterior ao início) são delegadas ao domínio.
/// SPEC §7.3.
/// </summary>
public sealed record EncerrarVigenciaLimiteGlobalBancoCommand(
    Guid Id,
    DateOnly DataFim) : IRequest<LimiteGlobalBancoDto>;

public sealed class EncerrarVigenciaLimiteGlobalBancoCommandValidator
    : AbstractValidator<EncerrarVigenciaLimiteGlobalBancoCommand>
{
    public EncerrarVigenciaLimiteGlobalBancoCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        // Guards against the default DateOnly value (0001-01-01) being submitted.
        RuleFor(c => c.DataFim)
            .Must(d => d != default)
            .WithMessage("DataFim deve ser uma data válida.");
    }
}

public sealed class EncerrarVigenciaLimiteGlobalBancoCommandHandler(
    ILimiteGlobalBancoRepository repo,
    IClock clock) : IRequestHandler<EncerrarVigenciaLimiteGlobalBancoCommand, LimiteGlobalBancoDto>
{
    public async Task<LimiteGlobalBancoDto> Handle(EncerrarVigenciaLimiteGlobalBancoCommand cmd, CancellationToken cancellationToken)
    {
        LimiteGlobalBanco limite = await repo.GetByIdTrackingAsync(cmd.Id, cancellationToken)
            ?? throw new InvalidOperationException($"LimiteGlobalBanco {cmd.Id} não encontrado.");

        // Domain enforces LG-08: rejects already-closed vigência and dates before DataVigenciaInicio.
        limite.EncerrarVigencia(
            new LocalDate(cmd.DataFim.Year, cmd.DataFim.Month, cmd.DataFim.Day),
            clock);

        await repo.SaveChangesAsync(cancellationToken);

        return LimiteGlobalBancoDto.From(limite);
    }
}
