using FluentValidation;
using MediatR;
using NodaTime;

using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Atualiza nome, descrição e/ou anoBase de um cenário existente.
/// Permitido em Rascunho e Ativo. Bloqueado em Arquivado (lança <see cref="InvalidOperationException"/>).
/// SPEC §7.4.
/// </summary>
public sealed record AtualizarCenarioCommand(
    Guid CenarioId,
    string? Nome,
    string? Descricao,
    int? AnoBase) : IRequest<CenarioSimulacaoDto>;

public sealed class AtualizarCenarioCommandValidator : AbstractValidator<AtualizarCenarioCommand>
{
    public AtualizarCenarioCommandValidator()
    {
        RuleFor(c => c.CenarioId).NotEmpty();

        When(c => c.Nome is not null, () =>
        {
            RuleFor(c => c.Nome!)
                .NotEmpty()
                .MaximumLength(100)
                .WithMessage("Nome não pode ser vazio nem exceder 100 caracteres.");
        });

        When(c => c.AnoBase.HasValue, () =>
        {
            RuleFor(c => c.AnoBase!.Value)
                .InclusiveBetween(2020, 2050)
                .WithMessage("AnoBase deve estar entre 2020 e 2050.");
        });
    }
}

public sealed class AtualizarCenarioCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock) : IRequestHandler<AtualizarCenarioCommand, CenarioSimulacaoDto>
{
    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        AtualizarCenarioCommand cmd,
        CancellationToken cancellationToken)
    {
        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        // Domínio lança InvalidOperationException se Arquivado.
        cenario.Atualizar(cmd.Nome, cmd.Descricao, cmd.AnoBase, clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        return CenarioSimulacaoDto.From(cenario);
    }
}
