using MediatR;
using Sgcf.Domain.Preferencias;

namespace Sgcf.Application.Preferencias.Commands;

/// <summary>
/// Remove a preferência identificada por (TenantId, UserId, Chave).
/// </summary>
/// <param name="UserId">Claim <c>sub</c> do JWT do usuário.</param>
/// <param name="Chave">Chave da preferência a remover.</param>
/// <exception cref="KeyNotFoundException">Quando a preferência não existe.</exception>
public sealed record DeletePreferenciaCommand(string UserId, string Chave)
    : IRequest;

/// <summary>
/// Handler de <see cref="DeletePreferenciaCommand"/>.
/// </summary>
public sealed class DeletePreferenciaCommandHandler(
    IPreferenciaUsuarioRepository repo) : IRequestHandler<DeletePreferenciaCommand>
{
    /// <inheritdoc />
    public async Task Handle(DeletePreferenciaCommand request, CancellationToken cancellationToken)
    {
        PreferenciaUsuario preferencia =
            await repo.GetAsync(request.UserId, request.Chave, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Preferência '{request.Chave}' não encontrada para o usuário.");

        repo.Remove(preferencia);
        await repo.SaveChangesAsync(cancellationToken);
    }
}
