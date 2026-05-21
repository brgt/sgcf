using MediatR;
using NodaTime;
using NodaTime.Text;

namespace Sgcf.Application.Tesouraria.Queries;

/// <summary>
/// Consulta os saldos de caixa de uma conta em um intervalo de datas.
/// </summary>
/// <param name="ContaId">Id da conta bancária.</param>
/// <param name="DateDe">Data de início do intervalo (ISO yyyy-MM-dd, inclusiva).</param>
/// <param name="DataAte">Data de fim do intervalo (ISO yyyy-MM-dd, inclusiva).</param>
public sealed record GetSaldoCaixaQuery(Guid ContaId, string DateDe, string DataAte)
    : IRequest<IReadOnlyList<SaldoCaixaDto>>;

public sealed class GetSaldoCaixaQueryHandler(ISaldoCaixaRepository repo)
    : IRequestHandler<GetSaldoCaixaQuery, IReadOnlyList<SaldoCaixaDto>>
{
    private static readonly LocalDatePattern IsoPattern = LocalDatePattern.Iso;

    public async Task<IReadOnlyList<SaldoCaixaDto>> Handle(
        GetSaldoCaixaQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate dataDe = IsoPattern.Parse(query.DateDe).Value;
        LocalDate dataAte = IsoPattern.Parse(query.DataAte).Value;

        IReadOnlyList<Domain.Tesouraria.SaldoCaixa> saldos = await repo.ListByContaAsync(
            query.ContaId, dataDe, dataAte, cancellationToken);

        return saldos.Select(SaldoCaixaDto.From).ToList().AsReadOnly();
    }
}
