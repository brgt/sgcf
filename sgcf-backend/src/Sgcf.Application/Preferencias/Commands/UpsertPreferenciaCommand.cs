using MediatR;
using NodaTime;
using Sgcf.Domain.Preferencias;

namespace Sgcf.Application.Preferencias.Commands;

/// <summary>
/// Cria ou atualiza a preferência identificada por (TenantId, UserId, Chave).
/// Se a preferência já existir, atualiza <c>Valor</c> e <c>AtualizadoEm</c>.
/// Caso contrário, cria uma nova entrada.
/// </summary>
/// <param name="UserId">Claim <c>sub</c> do JWT do usuário.</param>
/// <param name="Chave">Chave da preferência. Máximo 100 caracteres.</param>
/// <param name="Valor">Valor da preferência. Máximo 4000 caracteres.</param>
public sealed record UpsertPreferenciaCommand(string UserId, string Chave, string Valor)
    : IRequest<PreferenciaUsuarioDto>;

/// <summary>
/// Handler de <see cref="UpsertPreferenciaCommand"/>.
/// </summary>
public sealed class UpsertPreferenciaCommandHandler(
    IPreferenciaUsuarioRepository repo,
    IClock clock) : IRequestHandler<UpsertPreferenciaCommand, PreferenciaUsuarioDto>
{
    /// <inheritdoc />
    public async Task<PreferenciaUsuarioDto> Handle(
        UpsertPreferenciaCommand request,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        PreferenciaUsuario? existente =
            await repo.GetAsync(request.UserId, request.Chave, cancellationToken);

        if (existente is not null)
        {
            // Atualiza in-place — TenantSaveInterceptor não precisa agir novamente.
            existente.AtualizarValor(request.Valor, agora);
        }
        else
        {
            // Cria nova entrada — TenantId é preenchido pelo TenantSaveInterceptor no SaveChanges.
            PreferenciaUsuario nova = PreferenciaUsuario.Criar(request.UserId, request.Chave, request.Valor, agora);
            repo.Add(nova);
            existente = nova;
        }

        await repo.SaveChangesAsync(cancellationToken);

        return new PreferenciaUsuarioDto(existente.Chave, existente.Valor, existente.AtualizadoEm.ToString());
    }
}
