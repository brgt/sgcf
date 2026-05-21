using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Commands;

/// <summary>
/// Upsert de dados contábeis mensais: cria quando não existe, atualiza quando já existe.
/// </summary>
public sealed class UpsertDadosContabeisCommandHandler(
    IDadosContabeisRepository repo,
    IClock clock)
    : IRequestHandler<UpsertDadosContabeisCommand, Unit>
{
    public async Task<Unit> Handle(UpsertDadosContabeisCommand cmd, CancellationToken cancellationToken)
    {
        Money pl = new(cmd.PatrimonioLiquidoBrl, Moeda.Brl);
        Money despesa = new(cmd.DespesaFinanceiraBrl, Moeda.Brl);

        DadosContabeisMensal? existente = await repo.GetByCompetenciaAsync(cmd.Ano, cmd.Mes, cancellationToken);

        if (existente is not null)
        {
            existente.Atualizar(pl, despesa, clock);
            repo.Update(existente);
        }
        else
        {
            DadosContabeisMensal novo = DadosContabeisMensal.Criar(cmd.Ano, cmd.Mes, pl, despesa, clock);
            repo.Add(novo);
        }

        await repo.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
