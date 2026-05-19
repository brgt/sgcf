using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Sistema;

namespace Sgcf.Application.Sistema.Commands;

/// <summary>
/// Atualiza o tetão mensal de movimentação nos parâmetros do sistema.
/// </summary>
/// <param name="Valor">
/// Novo valor em BRL. <c>null</c> remove o limite (desabilita a validação).
/// </param>
public sealed record AtualizarTetaoMensalCommand(decimal? Valor)
    : IRequest<ParametrosSistemaDto>;

/// <summary>
/// Handler de <see cref="AtualizarTetaoMensalCommand"/>.
/// Persiste o novo valor via repositório singleton.
/// </summary>
public sealed class AtualizarTetaoMensalCommandHandler(
    IParametroSistemaRepository repo,
    IClock clock) : IRequestHandler<AtualizarTetaoMensalCommand, ParametrosSistemaDto>
{
    /// <inheritdoc />
    public async Task<ParametrosSistemaDto> Handle(
        AtualizarTetaoMensalCommand request,
        CancellationToken cancellationToken)
    {
        ParametroSistema parametros =
            await repo.GetOrCreateGlobalAsync(clock, cancellationToken);

        Money? novoValor = request.Valor.HasValue
            ? new Money(request.Valor.Value, Moeda.Brl)
            : null;

        // Lança ArgumentException ou ArgumentOutOfRangeException se inválido
        parametros.AtualizarTetaoMensal(novoValor, clock);

        await repo.SaveChangesAsync(cancellationToken);

        return new ParametrosSistemaDto(
            TetaoMensalCapacidadeBrl: parametros.TetaoMensalCapacidadeBrl?.Valor);
    }
}
