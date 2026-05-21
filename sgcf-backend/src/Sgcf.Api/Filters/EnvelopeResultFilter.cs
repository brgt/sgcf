using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using NodaTime;

using Sgcf.Application.Common;

namespace Sgcf.Api.Filters;

/// <summary>
/// Marca um endpoint para que o <see cref="EnvelopeResultFilter"/> envolva automaticamente
/// a resposta no formato <see cref="EnvelopeResponse{T}"/>.
///
/// Uso:
/// <code>
/// [HttpGet("meu-endpoint")]
/// [ProducesEnvelope]
/// public async Task&lt;IActionResult&gt; GetAsync(...) { ... }
/// </code>
///
/// O filtro só age quando este atributo está presente no endpoint.
/// Endpoints sem <c>[ProducesEnvelope]</c> permanecem inalterados.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class ProducesEnvelopeAttribute : Attribute { }

/// <summary>
/// Filtro de resultado que envolve a resposta de domínio em um <see cref="EnvelopeResponse{T}"/>
/// quando o endpoint declara <c>[ProducesEnvelope]</c>.
///
/// Comportamento:
/// <list type="bullet">
///   <item>Se o endpoint não tem <c>[ProducesEnvelope]</c> → passa sem modificar.</item>
///   <item>Se o resultado já é um <see cref="EnvelopeResponse{T}"/> (handler enriqueceu) → passa sem modificar.</item>
///   <item>Caso contrário, extrai o valor do <see cref="ObjectResult"/> e envolve em
///   <see cref="EnvelopeResponse{T}"/> com meta mínima (fontes vazias, completude Completo).</item>
/// </list>
///
/// O instante de cálculo é obtido via <c>IClock</c> (NodaTime) injetado por DI,
/// mantendo a regra de nunca usar <c>DateTime.UtcNow</c> em código de aplicação.
///
/// Registrado como filtro de escopo (<c>AddScoped</c>) para receber <c>IClock</c> via DI.
/// </summary>
public sealed class EnvelopeResultFilter(IClock clock) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Verifica se o endpoint (método ou controller) declara [ProducesEnvelope].
        // HasEffectiveAttribute percorre tanto o método quanto o tipo declarante.
        bool devEenvelopar = context.ActionDescriptor.EndpointMetadata
            .OfType<ProducesEnvelopeAttribute>()
            .Any();

        if (!devEenvelopar)
        {
            await next();
            return;
        }

        // Handler que já retornou EnvelopeResponse<T> não precisa ser re-envolvido.
        // Isso permite que handlers "ricos" forneçam fontes e completude customizadas.
        if (context.Result is ObjectResult { Value: { } value }
            && IsEnvelopeResponse(value.GetType()))
        {
            await next();
            return;
        }

        // Apenas ObjectResult com valor carrega dados de domínio para envolver.
        // Resultados como NoContentResult, RedirectResult, etc. são passados sem modificação.
        if (context.Result is not ObjectResult objectResult || objectResult.Value is null)
        {
            await next();
            return;
        }

        EnvelopeMeta meta = new(
            DataHoraCalculo:    clock.GetCurrentInstant(),
            FontesConsultadas:  [],
            Completude:         Completude.Completo);

        // Usa o tipo genérico aberto para construir EnvelopeResponse<TData> em runtime.
        // Necessário porque o tipo de Value só é conhecido em tempo de execução.
        Type envelopeType = typeof(EnvelopeResponse<>).MakeGenericType(objectResult.Value.GetType());
        object envelope   = Activator.CreateInstance(envelopeType, objectResult.Value, meta)!;

        context.Result = new ObjectResult(envelope)
        {
            StatusCode = objectResult.StatusCode
        };

        await next();
    }

    /// <summary>
    /// Retorna <c>true</c> quando <paramref name="type"/> é uma instância concreta de
    /// <see cref="EnvelopeResponse{T}"/> (ou seja, <c>typeof(EnvelopeResponse&lt;X&gt;)</c>).
    /// </summary>
    private static bool IsEnvelopeResponse(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EnvelopeResponse<>);
}
