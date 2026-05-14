using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Commands;

public sealed record UpdateContratoCommand(
    Guid ContratoId,
    string? NumeroExterno,
    decimal? TaxaAa,
    DateOnly? DataVencimento,
    string? Observacoes,
    string? BaseCalculo,
    string? Periodicidade,
    string? EstruturaAmortizacao,
    int? QuantidadeParcelas,
    DateOnly? DataPrimeiroVencimento,
    string? ConvencaoDataNaoUtil)
    : IRequest<ContratoDto>;

public sealed class UpdateContratoCommandValidator : AbstractValidator<UpdateContratoCommand>
{
    public UpdateContratoCommandValidator()
    {
        RuleFor(c => c.NumeroExterno)
            .NotEmpty()
            .When(c => c.NumeroExterno is not null)
            .WithMessage("NumeroExterno não pode ser vazio.");

        RuleFor(c => c.TaxaAa)
            .GreaterThan(0m)
            .When(c => c.TaxaAa.HasValue)
            .WithMessage("TaxaAa deve ser maior que zero.");

        RuleFor(c => c.BaseCalculo)
            .Must(v => Enum.TryParse<Domain.Common.BaseCalculo>(v, true, out _))
            .When(c => c.BaseCalculo is not null)
            .WithMessage($"BaseCalculo deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Common.BaseCalculo>())}.");

        RuleFor(c => c.Periodicidade)
            .Must(v => Enum.TryParse<Domain.Contratos.Periodicidade>(v, true, out _))
            .When(c => c.Periodicidade is not null)
            .WithMessage($"Periodicidade deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Contratos.Periodicidade>())}.");

        RuleFor(c => c.EstruturaAmortizacao)
            .Must(v => Enum.TryParse<Domain.Contratos.EstruturaAmortizacao>(v, true, out _))
            .When(c => c.EstruturaAmortizacao is not null)
            .WithMessage($"EstruturaAmortizacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Contratos.EstruturaAmortizacao>())}.");

        RuleFor(c => c.QuantidadeParcelas)
            .GreaterThanOrEqualTo(1)
            .When(c => c.QuantidadeParcelas.HasValue)
            .WithMessage("QuantidadeParcelas deve ser maior ou igual a 1.");

        RuleFor(c => c.ConvencaoDataNaoUtil)
            .Must(v => Enum.TryParse<Domain.Calendario.ConvencaoDataNaoUtil>(v, true, out _))
            .When(c => c.ConvencaoDataNaoUtil is not null)
            .WithMessage($"ConvencaoDataNaoUtil deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Calendario.ConvencaoDataNaoUtil>())}.");
    }
}

public sealed class UpdateContratoCommandHandler(IContratoRepository repo, IClock clock)
    : IRequestHandler<UpdateContratoCommand, ContratoDto>
{
    public async Task<ContratoDto> Handle(UpdateContratoCommand cmd, CancellationToken cancellationToken)
    {
        Contrato contrato = await repo.GetByIdAsync(cmd.ContratoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{cmd.ContratoId}' não encontrado.");

        Percentual? taxaAa = cmd.TaxaAa.HasValue
            ? Percentual.De(cmd.TaxaAa.Value)
            : (Percentual?)null;

        LocalDate? dataVencimento = cmd.DataVencimento.HasValue
            ? new LocalDate(cmd.DataVencimento.Value.Year, cmd.DataVencimento.Value.Month, cmd.DataVencimento.Value.Day)
            : (LocalDate?)null;

        LocalDate? dataPrimeiroVencimento = cmd.DataPrimeiroVencimento.HasValue
            ? new LocalDate(cmd.DataPrimeiroVencimento.Value.Year, cmd.DataPrimeiroVencimento.Value.Month, cmd.DataPrimeiroVencimento.Value.Day)
            : (LocalDate?)null;

        Domain.Common.BaseCalculo? baseCalculo = cmd.BaseCalculo is not null
            ? Enum.Parse<Domain.Common.BaseCalculo>(cmd.BaseCalculo, true)
            : (Domain.Common.BaseCalculo?)null;

        Domain.Contratos.Periodicidade? periodicidade = cmd.Periodicidade is not null
            ? Enum.Parse<Domain.Contratos.Periodicidade>(cmd.Periodicidade, true)
            : (Domain.Contratos.Periodicidade?)null;

        Domain.Contratos.EstruturaAmortizacao? estruturaAmortizacao = cmd.EstruturaAmortizacao is not null
            ? Enum.Parse<Domain.Contratos.EstruturaAmortizacao>(cmd.EstruturaAmortizacao, true)
            : (Domain.Contratos.EstruturaAmortizacao?)null;

        Domain.Calendario.ConvencaoDataNaoUtil? convencao = cmd.ConvencaoDataNaoUtil is not null
            ? Enum.Parse<Domain.Calendario.ConvencaoDataNaoUtil>(cmd.ConvencaoDataNaoUtil, true)
            : (Domain.Calendario.ConvencaoDataNaoUtil?)null;

        contrato.Atualizar(
            clock,
            numeroExterno: cmd.NumeroExterno,
            taxaAa: taxaAa,
            dataVencimento: dataVencimento,
            observacoes: cmd.Observacoes,
            baseCalculo: baseCalculo,
            periodicidade: periodicidade,
            estruturaAmortizacao: estruturaAmortizacao,
            quantidadeParcelas: cmd.QuantidadeParcelas,
            dataPrimeiroVencimento: dataPrimeiroVencimento,
            convencaoDataNaoUtil: convencao);

        await repo.SaveChangesAsync(cancellationToken);

        return ContratoDto.From(contrato, null, null, null, null, null, null);
    }
}
