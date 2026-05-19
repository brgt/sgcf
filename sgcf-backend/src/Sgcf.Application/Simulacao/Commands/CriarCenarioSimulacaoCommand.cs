using FluentValidation;
using MediatR;
using NodaTime;

using Sgcf.Application.Common;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Cria um novo cenário de simulação em status Rascunho.
/// O campo CriadoPor é resolvido via <see cref="ICurrentUserService"/>;
/// quando não houver usuário autenticado (ex: jobs internos), usa a constante "sistema".
/// SPEC §7.4.
/// </summary>
public sealed record CriarCenarioSimulacaoCommand(
    string Nome,
    int AnoBase,
    string? Descricao = null) : IRequest<CenarioSimulacaoDto>;

public sealed class CriarCenarioSimulacaoCommandValidator : AbstractValidator<CriarCenarioSimulacaoCommand>
{
    public CriarCenarioSimulacaoCommandValidator()
    {
        RuleFor(c => c.Nome)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Nome do cenário é obrigatório e não pode exceder 100 caracteres.");

        RuleFor(c => c.AnoBase)
            .InclusiveBetween(2020, 2050)
            .WithMessage("AnoBase deve estar entre 2020 e 2050.");
    }
}

public sealed class CriarCenarioSimulacaoCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock,
    ICurrentUserService? currentUser = null) : IRequestHandler<CriarCenarioSimulacaoCommand, CenarioSimulacaoDto>
{
    private const string UsuarioSistema = "sistema";

    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        CriarCenarioSimulacaoCommand cmd,
        CancellationToken cancellationToken)
    {
        string criadoPor = currentUser?.ActorSub ?? UsuarioSistema;

        CenarioSimulacao cenario = CenarioSimulacao.Criar(
            cmd.Nome,
            cmd.AnoBase,
            criadoPor,
            clock,
            cmd.Descricao);

        repo.Add(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        return CenarioSimulacaoDto.From(cenario);
    }
}
