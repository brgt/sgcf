using Sgcf.Application.Common;
using Sgcf.Domain.Auditoria;

namespace Sgcf.Infrastructure.Services;

/// <summary>
/// Implementação de <see cref="ICurrentUserService"/> usada em contextos sem HTTP
/// (jobs em background, seeds, migrations, testes de integração sem token).
/// Retorna <see cref="AuditConstants.SystemActor"/> para que o <c>audit_log</c>
/// registe alterações administrativas de forma distinguível de bugs de autenticação.
/// </summary>
internal sealed class SystemCurrentUserService : ICurrentUserService
{
    public string ActorSub  => AuditConstants.SystemActor;
    public string ActorRole => AuditConstants.SystemActor;
}
