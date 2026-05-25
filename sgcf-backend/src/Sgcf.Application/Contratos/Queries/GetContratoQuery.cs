using MediatR;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Contratos.Queries;

public sealed record GetContratoQuery(Guid Id) : IRequest<ContratoDto>;

public sealed class GetContratoQueryHandler(IContratoRepository repo, ILimiteBancoRepository limiteBancoRepo)
    : IRequestHandler<GetContratoQuery, ContratoDto>
{
    public async Task<ContratoDto> Handle(GetContratoQuery query, CancellationToken cancellationToken)
    {
        Contrato contrato = await repo.GetByIdWithDetailsAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{query.Id}' não encontrado.");

        FinimpDetail? finimpDetail = await repo.GetFinimpDetailAsync(query.Id, cancellationToken);
        Lei4131Detail? lei4131Detail = await repo.GetLei4131DetailAsync(query.Id, cancellationToken);
        RefinimpDetail? refinimpDetail = contrato.Modalidade == ModalidadeContrato.Refinimp
            ? await repo.GetRefinimpDetailAsync(query.Id, cancellationToken)
            : null;
        NceDetail? nceDetail = contrato.Modalidade == ModalidadeContrato.Nce
            ? await repo.GetNceDetailAsync(query.Id, cancellationToken)
            : null;
        CapitalDeGiroDetail? capitalDeGiroDetail = contrato.Modalidade == ModalidadeContrato.CapitalDeGiro
            ? await repo.GetCapitalDeGiroDetailAsync(query.Id, cancellationToken)
            : null;
        FgiDetail? fgiDetail = contrato.Modalidade == ModalidadeContrato.Fgi
            ? await repo.GetFgiDetailAsync(query.Id, cancellationToken)
            : null;

        // Snapshot de garantias: carregado apenas no detalhe (SPEC §5.2).
        // Só vale a pena buscar se o contrato tiver GarantiasExigidasRevisaoId preenchido.
        IReadOnlyCollection<GarantiaExigidaItem>? snapshotItens = null;
        if (contrato.GarantiasExigidasRevisaoId.HasValue)
        {
            // GetRevisoesGarantiasAsync retorna todas as revisões do LimiteBanco com seus itens.
            // Filtramos a revisão específica apontada pelo snapshot do contrato.
            // LimiteBancoId nunca é null quando GarantiasExigidasRevisaoId está preenchido (SC-01→SC-03).
            if (contrato.LimiteBancoId.HasValue)
            {
                IReadOnlyList<GarantiaExigidaRevisao> revisoes = await limiteBancoRepo
                    .GetRevisoesGarantiasAsync(contrato.LimiteBancoId.Value, cancellationToken);

                GarantiaExigidaRevisao? revisao = revisoes
                    .FirstOrDefault(r => r.Id == contrato.GarantiasExigidasRevisaoId.Value);

                snapshotItens = revisao?.Itens;
            }
        }

        return ContratoDto.From(contrato, finimpDetail, lei4131Detail, refinimpDetail, nceDetail, capitalDeGiroDetail, fgiDetail, snapshotItens);
    }
}
