namespace Sgcf.Application.Authorization;

public static class Policies
{
    public const string Leitura   = "Leitura";
    public const string Escrita   = "Escrita";
    public const string Gerencial = "Gerencial";
    public const string Executivo = "Executivo";
    public const string Auditoria = "Auditoria";
    public const string Admin      = "Admin";

    /// <summary>
    /// Super-admin Nordware: acesso cross-tenant a rotas de administração global.
    /// Mapeado para o role JWT "super-admin".
    /// </summary>
    public const string SuperAdmin = "SuperAdmin";
}
