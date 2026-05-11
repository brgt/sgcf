using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Sgcf.Api.Filters;

public sealed class IdempotencyFilter(IMemoryCache cache) : IAsyncActionFilter
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out StringValues keyValues))
        {
            await next();
            return;
        }

        string cacheKey = $"idempotency:{keyValues}";

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
