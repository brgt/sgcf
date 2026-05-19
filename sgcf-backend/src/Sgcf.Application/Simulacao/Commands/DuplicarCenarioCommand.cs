using MediatR;
using NodaTime;

using Sgcf.Application.Common;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Cria uma cópia profunda de um cenário existente em status Rascunho.
/// Regras (SPEC D-10 / Q7):
///   - Novo Id gerado automaticamente.
///   - Nome = "{origem.Nome} (cópia)".
///   - CriadoPor = usuário autenticado (ou "sistema").
///   - Todas as simulações filhas são copiadas com novos Ids e Version = 1.
///   - Cenários Arquivados também podem ser duplicados.
/// Lança <see cref="KeyNotFoundException"/> se o cenário origem não existir.
/// </summary>
public sealed record DuplicarCenarioCommand(Guid CenarioOrigemId) : IRequest<CenarioSimulacaoDto>;

public sealed class DuplicarCenarioCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock,
    ICurrentUserService? currentUser = null) : IRequestHandler<DuplicarCenarioCommand, CenarioSimulacaoDto>
{
    private const string UsuarioSistema = "sistema";

    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        DuplicarCenarioCommand cmd,
        CancellationToken cancellationToken)
    {
        CenarioSimulacao origem = await repo.GetByIdAsync(cmd.CenarioOrigemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário origem '{cmd.CenarioOrigemId}' não encontrado.");

        string novoCriadoPor = currentUser?.ActorSub ?? UsuarioSistema;

        CenarioSimulacao copia = CenarioSimulacao.DuplicarComoRascunho(origem, novoCriadoPor, clock);

        repo.Add(copia);
        await repo.SaveChangesAsync(cancellationToken);

        return CenarioSimulacaoDto.From(copia);
    }
}
