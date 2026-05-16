using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Atualiza campos básicos editáveis da cotação (PrazoMaximoDias, Observacoes).
/// Apenas permitido em status Rascunho. SPEC §7.1.
/// </summary>
public sealed record AtualizarCotacaoCommand(
    Guid CotacaoId,
    int? PrazoMaximoDias,
    string? Observacoes) : IRequest<CotacaoDto>;

public sealed class AtualizarCotacaoCommandValidator : AbstractValidator<AtualizarCotacaoCommand>
{
    public AtualizarCotacaoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();

        When(c => c.PrazoMaximoDias.HasValue, () =>
        {
            RuleFor(c => c.PrazoMaximoDias!.Value)
                .GreaterThanOrEqualTo(1)
                .WithMessage("PrazoMaximoDias deve ser maior ou igual a 1.");
        });
    }
}

public sealed class AtualizarCotacaoCommandHandler(ICotacaoRepository repo, IClock clock)
    : IRequestHandler<AtualizarCotacaoCommand, CotacaoDto>
{
    public async Task<CotacaoDto> Handle(AtualizarCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        cotacao.EditarCamposBasicos(cmd.PrazoMaximoDias, cmd.Observacoes, clock);
        await repo.SaveChangesAsync(cancellationToken);

        return CotacaoDto.From(cotacao);
    }
}
