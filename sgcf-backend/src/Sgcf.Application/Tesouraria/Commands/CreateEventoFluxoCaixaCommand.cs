using FluentValidation;
using MediatR;
using NodaTime;
using NodaTime.Text;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria.Commands;

/// <summary>
/// Cria em lote um ou mais eventos manuais de fluxo de caixa.
/// </summary>
public sealed record CreateEventoFluxoCaixaCommand(
    IReadOnlyList<CreateEventoFluxoCaixaItemDto> Itens)
    : IRequest<IReadOnlyList<EventoFluxoCaixaDto>>;

public sealed class CreateEventoFluxoCaixaCommandValidator
    : AbstractValidator<CreateEventoFluxoCaixaCommand>
{
    private static readonly LocalDatePattern IsoPattern = LocalDatePattern.Iso;

    public CreateEventoFluxoCaixaCommandValidator()
    {
        RuleFor(c => c.Itens)
            .NotEmpty()
            .WithMessage("A lista de itens não pode ser vazia.");

        RuleForEach(c => c.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Data)
                .NotEmpty()
                .Must(d => IsoPattern.Parse(d).Success)
                .WithMessage("Data deve ser uma data ISO válida (yyyy-MM-dd).");

            item.RuleFor(i => i.Valor)
                .GreaterThan(0m)
                .WithMessage("Valor deve ser maior que zero.");

            item.RuleFor(i => i.Tipo)
                .NotEmpty()
                .Must(t => Enum.TryParse<TipoEventoFluxo>(t, ignoreCase: true, out _))
                .WithMessage($"Tipo deve ser um dos valores: {string.Join(", ", Enum.GetNames<TipoEventoFluxo>())}.");

            item.RuleFor(i => i.Moeda)
                .NotEmpty()
                .Must(m => Enum.TryParse<Moeda>(m, ignoreCase: true, out _))
                .WithMessage($"Moeda deve ser um dos valores: {string.Join(", ", Enum.GetNames<Moeda>())}.");

            item.RuleFor(i => i.Descricao)
                .NotEmpty()
                .MaximumLength(500)
                .WithMessage("Descrição não pode exceder 500 caracteres.");

            item.RuleFor(i => i.RegistradoPor)
                .NotEmpty()
                .WithMessage("RegistradoPor não pode ser vazio.");
        });
    }
}

public sealed class CreateEventoFluxoCaixaCommandHandler(
    IEventoFluxoCaixaRepository repo,
    IClock clock)
    : IRequestHandler<CreateEventoFluxoCaixaCommand, IReadOnlyList<EventoFluxoCaixaDto>>
{
    private static readonly LocalDatePattern IsoPattern = LocalDatePattern.Iso;

    public async Task<IReadOnlyList<EventoFluxoCaixaDto>> Handle(
        CreateEventoFluxoCaixaCommand command,
        CancellationToken cancellationToken)
    {
        List<EventoFluxoCaixaDto> resultado = new(command.Itens.Count);

        foreach (CreateEventoFluxoCaixaItemDto item in command.Itens)
        {
            LocalDate data = IsoPattern.Parse(item.Data).Value;
            TipoEventoFluxo tipo = Enum.Parse<TipoEventoFluxo>(item.Tipo, ignoreCase: true);
            Moeda moeda = Enum.Parse<Moeda>(item.Moeda, ignoreCase: true);
            Money valor = new(item.Valor, moeda);

            EventoFluxoCaixa evento = EventoFluxoCaixa.Criar(
                data, tipo, valor, item.Descricao, item.RegistradoPor, clock);

            await repo.AddAsync(evento, cancellationToken);

            resultado.Add(EventoFluxoCaixaDto.From(evento));
        }

        await repo.SaveChangesAsync(cancellationToken);

        return resultado.AsReadOnly();
    }
}
