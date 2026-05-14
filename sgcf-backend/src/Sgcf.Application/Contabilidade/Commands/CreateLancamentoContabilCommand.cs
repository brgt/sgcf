using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Commands;

public sealed record CreateLancamentoContabilCommand(
    Guid ContratoId,
    DateOnly Data,
    string Origem,
    decimal ValorDecimal,
    string Moeda,
    string Descricao)
    : IRequest<LancamentoContabilDto>;

public sealed class CreateLancamentoContabilCommandValidator : AbstractValidator<CreateLancamentoContabilCommand>
{
    public CreateLancamentoContabilCommandValidator()
    {
        RuleFor(c => c.ContratoId)
            .NotEmpty()
            .WithMessage("ContratoId não pode ser vazio.");

        RuleFor(c => c.Origem)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("Origem não pode ser vazia e deve ter no máximo 50 caracteres.");

        RuleFor(c => c.ValorDecimal)
            .GreaterThan(0)
            .WithMessage("Valor deve ser maior que zero.");

        RuleFor(c => c.Moeda)
            .NotEmpty()
            .Must(v => Enum.TryParse<Moeda>(v, true, out _))
            .WithMessage($"Moeda deve ser um dos valores: {string.Join(", ", Enum.GetNames<Moeda>())}.");

        RuleFor(c => c.Descricao)
            .NotEmpty()
            .WithMessage("Descricao não pode ser vazia.");
    }
}

public sealed class CreateLancamentoContabilCommandHandler(
    ILancamentoContabilRepository repo,
    IClock clock)
    : IRequestHandler<CreateLancamentoContabilCommand, LancamentoContabilDto>
{
    public async Task<LancamentoContabilDto> Handle(
        CreateLancamentoContabilCommand cmd,
        CancellationToken cancellationToken)
    {
        Moeda moeda = Enum.Parse<Moeda>(cmd.Moeda, true);
        LocalDate data = new(cmd.Data.Year, cmd.Data.Month, cmd.Data.Day);
        Money valor = new(cmd.ValorDecimal, moeda);

        LancamentoContabil lancamento = LancamentoContabil.Criar(
            cmd.ContratoId,
            data,
            cmd.Origem,
            valor,
            cmd.Descricao,
            clock);

        repo.Add(lancamento);
        await repo.SaveChangesAsync(cancellationToken);

        return LancamentoContabilDto.From(lancamento);
    }
}
