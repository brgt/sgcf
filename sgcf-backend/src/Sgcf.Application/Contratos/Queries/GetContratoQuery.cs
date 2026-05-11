using MediatR;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Queries;

public sealed record GetContratoQuery(Guid Id) : IRequest<ContratoDto>;

public sealed class GetContratoQueryHandler(IContratoRepository repo)
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
        BalcaoCaixaDetail? balcaoCaixaDetail = contrato.Modalidade == ModalidadeContrato.BalcaoCaixa
            ? await repo.GetBalcaoCaixaDetailAsync(query.Id, cancellationToken)
            : null;
        FgiDetail? fgiDetail = contrato.Modalidade == ModalidadeContrato.Fgi
            ? await repo.GetFgiDetailAsync(query.Id, cancellationToken)
            : null;

        return ContratoDto.From(contrato, finimpDetail, lei4131Detail, refinimpDetail, nceDetail, balcaoCaixaDetail, fgiDetail);
    }
}
