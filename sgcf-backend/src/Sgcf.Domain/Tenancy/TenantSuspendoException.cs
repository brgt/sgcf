namespace Sgcf.Domain.Tenancy;

public sealed class TenantSuspendoException(string slug)
    : InvalidOperationException($"Tenant '{slug}' está suspenso. Reative o tenant antes de provisionar.");
