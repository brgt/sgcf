namespace Sgcf.Api.Middleware;

/// <summary>
/// Catálogo de <c>type</c> URIs estáveis de erro (RFC 7807). Base no domínio do produto. SPEC S40 §5.2.
/// Os URIs fazem parte do contrato e não devem mudar; o front-end ramifica por eles.
/// </summary>
internal static class ProblemTypes
{
    private const string Base = "https://sgcf.nordware.io/errors/";

    public const string Validacao = Base + "validacao";
    public const string NaoEncontrado = Base + "nao-encontrado";
    public const string ConflitoDeEstado = Base + "conflito-de-estado";
    public const string PtaxIndisponivel = Base + "ptax-indisponivel";
    public const string EntidadeNaoProcessavel = Base + "entidade-nao-processavel";
    public const string Interno = Base + "interno";

    // Follow-up (consumido pelo FE): migrar de sgcf.io para a base sgcf.nordware.io. SPEC S40 §5.3.
    public const string GarantiaExigidaNaoCoberta = "https://sgcf.io/errors/garantia-exigida-nao-coberta";
}
