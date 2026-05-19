using MediatR;
using NodaTime;

namespace Sgcf.Application.Sistema.Queries;

/// <summary>Retorna os parâmetros globais do sistema (singleton).</summary>
public sealed record GetParametrosSistemaQuery : IRequest<ParametrosSistemaDto>;

/// <summary>
/// Handler de <see cref="GetParametrosSistemaQuery"/>.
/// Lê (ou cria) o singleton e mapeia para DTO.
/// </summary>
public sealed class GetParametrosSistemaQueryHandler(
    IParametroSistemaRepository repo,
    IClock clock) : IRequestHandler<GetParametrosSistemaQuery, ParametrosSistemaDto>
{
    /// <inheritdoc />
    public async Task<ParametrosSistemaDto> Handle(
        GetParametrosSistemaQuery request,
        CancellationToken cancellationToken)
    {
        Domain.Sistema.ParametroSistema parametros =
            await repo.GetOrCreateGlobalAsync(clock, cancellationToken);

        return new ParametrosSistemaDto(
            TetaoMensalCapacidadeBrl: parametros.TetaoMensalCapacidadeBrl?.Valor);
    }
}
