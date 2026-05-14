using FluentValidation;

using MediatR;

using NodaTime;

using Sgcf.Domain.Calendario;

namespace Sgcf.Application.Calendario.Commands;

public sealed record CreateFeriadoCommand(
    DateOnly Data,
    string Descricao,
    string Abrangencia,
    string Tipo)
    : IRequest<FeriadoDto>;

public sealed class CreateFeriadoCommandValidator : AbstractValidator<CreateFeriadoCommand>
{
    public CreateFeriadoCommandValidator()
    {
        RuleFor(c => c.Descricao)
            .NotEmpty()
            .WithMessage("Descricao não pode ser vazia.");

        RuleFor(c => c.Abrangencia)
            .NotEmpty()
            .Must(v => Enum.TryParse<EscopoFeriado>(v, true, out _))
            .WithMessage($"Abrangencia deve ser um dos valores: {string.Join(", ", Enum.GetNames<EscopoFeriado>())}.");

        RuleFor(c => c.Tipo)
            .NotEmpty()
            .Must(v => Enum.TryParse<TipoFeriado>(v, true, out _))
            .WithMessage($"Tipo deve ser um dos valores: {string.Join(", ", Enum.GetNames<TipoFeriado>())}.");
    }
}

public sealed class CreateFeriadoCommandHandler(IFeriadoRepository repo, IClock clock)
    : IRequestHandler<CreateFeriadoCommand, FeriadoDto>
{
    public async Task<FeriadoDto> Handle(
        CreateFeriadoCommand cmd,
        CancellationToken cancellationToken)
    {
        LocalDate data = new(cmd.Data.Year, cmd.Data.Month, cmd.Data.Day);
        EscopoFeriado escopo = Enum.Parse<EscopoFeriado>(cmd.Abrangencia, true);
        TipoFeriado tipo = Enum.Parse<TipoFeriado>(cmd.Tipo, true);

        Feriado feriado = Feriado.Criar(
            data,
            tipo,
            escopo,
            cmd.Descricao,
            FonteFeriado.Manual,
            data.Year,
            clock);

        repo.Add(feriado);
        await repo.SaveChangesAsync(cancellationToken);

        return FeriadoDto.From(feriado);
    }
}
