using MediatR;
using NodaTime;
using Sgcf.Domain.Painel;

namespace Sgcf.Application.Painel.Commands;

/// <summary>
/// Upsert do EBITDA mensal: cria quando não existe, atualiza quando já existe.
/// </summary>
public sealed class UpsertEbitdaMensalCommandHandler(
    IEbitdaMensalRepository ebitdaRepo,
    IClock clock)
    : IRequestHandler<UpsertEbitdaMensalCommand, Unit>
{
    public async Task<Unit> Handle(UpsertEbitdaMensalCommand cmd, CancellationToken cancellationToken)
    {
        EbitdaMensal? existente = await ebitdaRepo.GetAsync(cmd.Ano, cmd.Mes, cancellationToken);

        if (existente is not null)
        {
            existente.Atualizar(cmd.ValorBrl, cmd.CreatedBy, clock);
            ebitdaRepo.Update(existente);
        }
        else
        {
            EbitdaMensal novo = EbitdaMensal.Criar(cmd.Ano, cmd.Mes, cmd.ValorBrl, cmd.CreatedBy, clock);
            ebitdaRepo.Add(novo);
        }

        await ebitdaRepo.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
