using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Sgcf.Application.Tests")]
// Jobs resolve TenantContext diretamente para operações cross-tenant em background.
[assembly: InternalsVisibleTo("Sgcf.Jobs")]
