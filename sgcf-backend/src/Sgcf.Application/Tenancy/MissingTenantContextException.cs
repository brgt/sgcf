namespace Sgcf.Application.Tenancy;

/// <summary>
/// Lançada quando código tenta acessar <see cref="ITenantContext"/> antes do
/// <c>TenantResolverMiddleware</c> ter resolvido o tenant para o escopo corrente.
/// Indica bug de programação (acesso antes do middleware executar) ou rota
/// que deveria ter sido marcada como bypass.
/// </summary>
public sealed class MissingTenantContextException(string message) : InvalidOperationException(message);
