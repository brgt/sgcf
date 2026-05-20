using System.Security.Claims;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Sgcf.Api.Filters;

/// <summary>
/// Filtro de idempotência para operações POST que criam recursos.
///
/// Comportamento:
///   1. Se o header <c>Idempotency-Key</c> estiver ausente, a requisição segue normalmente.
///   2. Se a key tiver formato inválido, retorna 400 Bad Request imediatamente.
///   3. Se a key for válida e já existir no cache com o mesmo escopo, retorna a resposta
///      em cache sem executar o handler (deduplicação).
///   4. Caso contrário, executa o handler e armazena a resposta 2xx no cache.
///
/// Formato aceito para <c>Idempotency-Key</c>:
///   - UUID v4 no formato padrão (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx), OU
///   - String alfanumérica + hífens/underscores de 1–64 caracteres.
///
/// Cache key composta:
///   <code>idempotency:{userSub}:{method}:{path}:{key}</code>
///
/// O escopo por usuário + método + path previne que:
///   - Dois usuários diferentes com a mesma key recebam respostas cruzadas (IDOR).
///   - Um GET com a mesma key de um POST receba o body do POST.
///   - Uma rota diferente com a mesma key acesse respostas de outra rota.
/// </summary>
public sealed partial class IdempotencyFilter(IMemoryCache cache) : IAsyncActionFilter
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    /// <summary>
    /// Aceita UUID v4 canônico (com hífens) OU string alfanumérica com hífens/underscores
    /// de 1 a 64 caracteres. Rejeita path-traversal, espaços, e caracteres especiais.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex ChaveValida();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out StringValues keyValues))
        {
            await next();
            return;
        }

        string key = keyValues.ToString();

        // Rejeita keys com formato inválido antes de qualquer acesso ao cache.
        // Previne cache poisoning e garante que clientes enviem keys controláveis.
        if (!ChaveValida().IsMatch(key))
        {
            context.Result = new BadRequestObjectResult(new
            {
                type   = "https://tools.ietf.org/html/rfc7807",
                title  = "Idempotency-Key inválida.",
                status = 400,
                detail = "O header Idempotency-Key deve ser um UUID v4 (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx) " +
                         "ou uma string alfanumérica de 1 a 64 caracteres (A-Z, a-z, 0-9, hífens, underscores)."
            });
            return;
        }

        // Escopo completo: userSub + método HTTP + path + key.
        // Sem o escopo de usuário, dois usuários com a mesma key trocariam respostas (IDOR).
        // Sem o escopo de método/path, um GET poderia receber o body de um POST anterior.
        string userSub = context.HttpContext.User.FindFirst("sub")?.Value
            ?? context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";

        string method = context.HttpContext.Request.Method;
        string path   = context.HttpContext.Request.Path.Value ?? string.Empty;

        string cacheKey = $"idempotency:{userSub}:{method}:{path}:{key}";

        if (cache.TryGetValue(cacheKey, out object? cached))
        {
            context.Result = new OkObjectResult(cached);
            return;
        }

        ActionExecutedContext executed = await next();

        if (executed.Result is ObjectResult { StatusCode: >= 200 and < 300 } ok)
        {
            cache.Set(cacheKey, ok.Value, Ttl);
        }
    }
}
