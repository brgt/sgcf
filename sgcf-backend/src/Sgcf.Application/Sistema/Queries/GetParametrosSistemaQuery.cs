using MediatR;

namespace Sgcf.Application.Sistema.Queries;

/// <summary>
/// Retorna os parâmetros de sistema do tenant atual.
/// Retorna <c>null</c> quando o tenant não está provisionado.
/// </summary>
public sealed record GetParametrosSistemaQuery : IRequest<ParametrosSistemaDto?>;

/// <summary>
/// Handler de <see cref="GetParametrosSistemaQuery"/>.
/// Lê o registro per-tenant e mapeia para DTO.
/// </summary>
public sealed class GetParametrosSistemaQueryHandler(
    IParametroSistemaRepository repo) : IRequestHandler<GetParametrosSistemaQuery, ParametrosSistemaDto?>
{
    /// <inheritdoc />
    public async Task<ParametrosSistemaDto?> Handle(
        GetParametrosSistemaQuery request,
        CancellationToken cancellationToken)
    {
        Domain.Sistema.ParametroSistema? parametros =
            await repo.GetAsync(cancellationToken);

        if (parametros is null)
        {
            return null;
        }

        return new ParametrosSistemaDto(
            TetaoMensalCapacidadeBrl: parametros.TetaoMensalCapacidadeBrl?.Valor);
    }
}
