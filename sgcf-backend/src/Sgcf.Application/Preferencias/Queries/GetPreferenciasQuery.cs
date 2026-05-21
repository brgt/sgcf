using MediatR;
using Sgcf.Application.Common;
using Sgcf.Domain.Preferencias;

namespace Sgcf.Application.Preferencias.Queries;

/// <summary>
/// Retorna todas as preferências de UI do usuário no tenant atual.
/// </summary>
/// <param name="UserId">Claim <c>sub</c> do JWT do usuário solicitante.</param>
public sealed record GetPreferenciasQuery(string UserId)
    : IRequest<EnvelopeResponse<IReadOnlyList<PreferenciaUsuarioDto>>>;

/// <summary>
/// Handler de <see cref="GetPreferenciasQuery"/>.
/// </summary>
public sealed class GetPreferenciasQueryHandler(
    IPreferenciaUsuarioRepository repo,
    NodaTime.IClock clock) : IRequestHandler<GetPreferenciasQuery, EnvelopeResponse<IReadOnlyList<PreferenciaUsuarioDto>>>
{
    /// <inheritdoc />
    public async Task<EnvelopeResponse<IReadOnlyList<PreferenciaUsuarioDto>>> Handle(
        GetPreferenciasQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PreferenciaUsuario> preferencias =
            await repo.ListByUserIdAsync(request.UserId, cancellationToken);

        IReadOnlyList<PreferenciaUsuarioDto> dtos = preferencias
            .Select(p => new PreferenciaUsuarioDto(p.Chave, p.Valor, p.AtualizadoEm.ToString()))
            .ToList()
            .AsReadOnly();

        EnvelopeMeta meta = new(
            DataHoraCalculo: clock.GetCurrentInstant(),
            FontesConsultadas: [new FonteConsultada("banco_de_dados", "ok", preferencias.Count)],
            Completude: Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<PreferenciaUsuarioDto>>(dtos, meta);
    }
}
