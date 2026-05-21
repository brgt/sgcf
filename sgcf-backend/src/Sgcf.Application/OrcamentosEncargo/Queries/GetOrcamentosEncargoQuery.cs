using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.OrcamentosEncargo;

namespace Sgcf.Application.OrcamentosEncargo.Queries;

/// <summary>
/// Consulta orçamentos de encargo dentro de um intervalo de competência,
/// com filtros opcionais por banco e tipo de encargo.
/// </summary>
/// <param name="DeAno">Ano inicial do intervalo (inclusive).</param>
/// <param name="DeMes">Mês inicial do intervalo (inclusive, 1–12).</param>
/// <param name="AteAno">Ano final do intervalo (inclusive).</param>
/// <param name="AteMes">Mês final do intervalo (inclusive, 1–12).</param>
/// <param name="BancoId">Filtra por banco. Opcional.</param>
/// <param name="TipoEncargo">Filtra por tipo de encargo. Opcional.</param>
public sealed record GetOrcamentosEncargoQuery(
    int DeAno,
    int DeMes,
    int AteAno,
    int AteMes,
    Guid? BancoId = null,
    string? TipoEncargo = null) : IRequest<EnvelopeResponse<IReadOnlyList<OrcamentoEncargoDto>>>;

/// <summary>
/// Handler que consulta e projeta orçamentos de encargo no intervalo solicitado.
/// </summary>
public sealed class GetOrcamentosEncargoQueryHandler(
    IOrcamentoEncargoRepository repository,
    IClock clock)
    : IRequestHandler<GetOrcamentosEncargoQuery, EnvelopeResponse<IReadOnlyList<OrcamentoEncargoDto>>>
{
    public async Task<EnvelopeResponse<IReadOnlyList<OrcamentoEncargoDto>>> Handle(
        GetOrcamentosEncargoQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        IReadOnlyList<OrcamentoEncargo> orcamentos = await repository.ListAsync(
            query.DeAno,
            query.DeMes,
            query.AteAno,
            query.AteMes,
            query.BancoId,
            query.TipoEncargo,
            cancellationToken);

        List<OrcamentoEncargoDto> dtos = orcamentos
            .Select(o => new OrcamentoEncargoDto(
                o.Id,
                o.Ano,
                o.Mes,
                o.TipoEncargo,
                o.ValorOrcadoBrl.Valor,
                o.BancoId,
                o.ContratoId,
                o.Observacao))
            .ToList();

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("banco_de_dados", "ok", dtos.Count)],
            Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<OrcamentoEncargoDto>>(dtos.AsReadOnly(), meta);
    }
}
