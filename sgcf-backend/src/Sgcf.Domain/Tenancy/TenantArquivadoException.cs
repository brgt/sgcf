namespace Sgcf.Domain.Tenancy;

public sealed class TenantArquivadoException(string slug)
    : InvalidOperationException($"Tenant '{slug}' está arquivado e não pode ser provisionado.");
