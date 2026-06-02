using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;

namespace Sgcf.Application.Cambio.Commands;

/// <summary>
/// Registra manualmente uma cotação cambial (PTAX). Uso administrativo / contingência
/// quando a ingestão automática do BCB não está disponível. Grava preferencialmente
/// <see cref="TipoCotacao.PtaxD0"/> (coerente com o ingestor); a leitura D-1 resolve a
/// partir dele. Idempotente pela chave única (moeda_base, moeda_quote, momento, tipo).
/// SPEC §4.2 (RF-06/07/08).
/// </summary>
public sealed record RegistrarCotacaoFxCommand(
    string MoedaBase,
    DateTimeOffset Momento,
    decimal ValorCompra,
    decimal ValorVenda,
    string MoedaQuote = "Brl",
    string Tipo = "PtaxD0",
    string Fonte = "MANUAL") : IRequest<CotacaoFxDto>;

public sealed class RegistrarCotacaoFxCommandValidator : AbstractValidator<RegistrarCotacaoFxCommand>
{
    public RegistrarCotacaoFxCommandValidator(IClock clock)
    {
        RuleFor(c => c.ValorCompra)
            .GreaterThan(0m).WithMessage("ValorCompra deve ser maior que zero.");

        RuleFor(c => c.ValorVenda)
            .GreaterThan(0m).WithMessage("ValorVenda deve ser maior que zero.");

        RuleFor(c => c.MoedaBase)
            .NotEmpty()
            .Must(v => Enum.TryParse<Moeda>(v, true, out Moeda m) && m != Moeda.Brl)
            .WithMessage($"MoedaBase deve ser uma moeda estrangeira: {string.Join(", ", Enum.GetNames<Moeda>().Where(n => n != nameof(Moeda.Brl)))}.");

        RuleFor(c => c.MoedaQuote)
            .NotEmpty()
            .Must(v => Enum.TryParse<Moeda>(v, true, out Moeda m) && m == Moeda.Brl)
            .WithMessage("MoedaQuote deve ser BRL.");

        RuleFor(c => c.Tipo)
            .NotEmpty()
            .Must(v => Enum.TryParse<TipoCotacao>(v, true, out _))
            .WithMessage($"Tipo deve ser um dos valores: {string.Join(", ", Enum.GetNames<TipoCotacao>())}.");

        RuleFor(c => c.Fonte)
            .NotEmpty().WithMessage("Fonte não pode ser vazia.");

        RuleFor(c => c.Momento)
            .Must(momento => momento <= clock.GetCurrentInstant().ToDateTimeOffset())
            .WithMessage("Momento não pode ser no futuro.");
    }
}

public sealed class RegistrarCotacaoFxCommandHandler(ICotacaoFxRepository repo)
    : IRequestHandler<RegistrarCotacaoFxCommand, CotacaoFxDto>
{
    public async Task<CotacaoFxDto> Handle(RegistrarCotacaoFxCommand cmd, CancellationToken cancellationToken)
    {
        Moeda moedaBase = Enum.Parse<Moeda>(cmd.MoedaBase, true);
        Moeda moedaQuote = Enum.Parse<Moeda>(cmd.MoedaQuote, true);
        TipoCotacao tipo = Enum.Parse<TipoCotacao>(cmd.Tipo, true);
        Instant momento = Instant.FromDateTimeOffset(cmd.Momento);

        CotacaoFx cotacao = CotacaoFx.Criar(
            moedaBase,
            tipo,
            new Money(cmd.ValorCompra, moedaQuote),
            new Money(cmd.ValorVenda, moedaQuote),
            cmd.Fonte,
            momento);

        // UpsertAsync é idempotente pela unique key (moeda_base, moeda_quote, momento, tipo).
        await repo.UpsertAsync(cotacao, cancellationToken);

        return CotacaoFxDto.From(cotacao);
    }
}
