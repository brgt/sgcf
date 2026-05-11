using MediatR;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna o painel consolidado de dívida com breakdown por moeda, ajuste MTM e alertas
/// de contratos sem hedge vinculado. Filtros opcionais por banco e/ou modalidade.
/// </summary>
public sealed record GetPainelDividaQuery(
    Guid? BancoId = null,
    string? Modalidade = null) : IRequest<PainelDividaDto>;
