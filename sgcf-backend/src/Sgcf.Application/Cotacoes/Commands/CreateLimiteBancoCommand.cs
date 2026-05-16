using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>Cria limite operacional para banco/modalidade. SPEC §6.1.</summary>
public sealed record CreateLimiteBancoCommand(
    Guid BancoId,
    string Modalidade,
    decimal ValorLimiteBrl,
    DateOnly DataVigenciaInicio,
    DateOnly? DataVigenciaFim = null,
    string? Observacoes = null) : IRequest<LimiteBancoDto>;

public sealed class CreateLimiteBancoCommandValidator : AbstractValidator<CreateLimiteBancoCommand>
{
    public CreateLimiteBancoCommandValidator()
    {
        RuleFor(c => c.BancoId).NotEmpty();

        RuleFor(c => c.Modalidade)
            .NotEmpty()
            .Must(v => Enum.TryParse<ModalidadeContrato>(v, true, out _))
            .WithMessage($"Modalidade deve ser um dos valores: {string.Join(", ", Enum.GetNames<ModalidadeContrato>())}.");

        RuleFor(c => c.ValorLimiteBrl)
            .GreaterThan(0m)
            .WithMessage("ValorLimiteBrl deve ser maior que zero.");
    }
}

public sealed class CreateLimiteBancoCommandHandler(ILimiteBancoRepository repo, IClock clock)
    : IRequestHandler<CreateLimiteBancoCommand, LimiteBancoDto>
{
    public async Task<LimiteBancoDto> Handle(CreateLimiteBancoCommand cmd, CancellationToken cancellationToken)
    {
        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);
        LocalDate inicio = new(cmd.DataVigenciaInicio.Year, cmd.DataVigenciaInicio.Month, cmd.DataVigenciaInicio.Day);
        LocalDate? fim = cmd.DataVigenciaFim.HasValue
            ? new LocalDate(cmd.DataVigenciaFim.Value.Year, cmd.DataVigenciaFim.Value.Month, cmd.DataVigenciaFim.Value.Day)
            : null;

        Money valorLimite = new(cmd.ValorLimiteBrl, Moeda.Brl);

        LimiteBanco limite = LimiteBanco.Criar(
            cmd.BancoId,
            modalidade,
            valorLimite,
            inicio,
            clock,
            fim,
            cmd.Observacoes);

        repo.Add(limite);
        await repo.SaveChangesAsync(cancellationToken);

        return LimiteBancoDto.From(limite);
    }
}
