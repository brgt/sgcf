using System.Globalization;

using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Cotacoes.Exceptions;

namespace Sgcf.Api.Middleware;

internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception: {Message}")]
    private static partial void LogUnhandledException(ILogger logger, string message, Exception exception);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception.Message, exception);

        if (exception is ValidationException validationException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            Dictionary<string, string[]> errors = validationException.Errors
                .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => f.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            ValidationProblemDetails problemDetails = new(errors)
            {
                Type = ProblemTypes.Validacao,
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        if (exception is KeyNotFoundException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

            ProblemDetails problemDetails = new()
            {
                Type = ProblemTypes.NaoEncontrado,
                Title = "Resource not found",
                Status = StatusCodes.Status404NotFound,
                Detail = exception.Message,
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        // S40 §6: PTAX indisponível — type estável + extensões moedaAlvo/dataPtaxReferencia.
        if (exception is PtaxIndisponivelException ptaxEx)
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

            ProblemDetails problem = new()
            {
                Type = ProblemTypes.PtaxIndisponivel,
                Title = "PTAX indisponível",
                Status = StatusCodes.Status409Conflict,
                Detail = ptaxEx.Message,
            };
            problem.Extensions["moedaAlvo"] = ptaxEx.MoedaAlvo;
            problem.Extensions["dataPtaxReferencia"] = ptaxEx.DataReferencia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        if (exception is GarantiaExigidaNaoCobertaException garantiaEx)
        {
            // SC-04 — conversão bloqueada por garantia obrigatória sem cobertura. SPEC §4.5.
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

            ProblemDetails problem = new()
            {
                Type = ProblemTypes.GarantiaExigidaNaoCoberta,
                Title = "Garantias exigidas pela política do banco não foram cobertas pelo contrato.",
                Status = StatusCodes.Status409Conflict,
                Detail = $"A revisão vigente do LimiteBanco {garantiaEx.LimiteBancoId} exige " +
                         $"{garantiaEx.Lacunas.Count} garantia(s) obrigatória(s) que não foram supridas.",
            };
            // Exposição intencional: API é exclusivamente interna (operadores da Proxys).
            // Os IDs permitem consulta direta pelo operador sem round-trip adicional.
            problem.Extensions["limiteBancoId"] = garantiaEx.LimiteBancoId;
            problem.Extensions["garantiasExigidasRevisaoId"] = garantiaEx.GarantiasExigidasRevisaoId;
            problem.Extensions["lacunas"] = garantiaEx.Lacunas.Select(l => new
            {
                tipo = l.Tipo,
                obrigatoria = l.Obrigatoria,
                valorEsperadoBrl = l.ValorEsperadoBrl,
                valorCobertoBrl = l.ValorCobertoBrl,
                // Garantias alternativas (grupos "OU", RF-10): null para lacunas de item.
                grupoAlternativaId = l.GrupoAlternativaId,
                grupoRotulo = l.GrupoRotulo,
                alternativasAceitas = l.AlternativasAceitas,
                fracaoCoberta = l.FracaoCoberta,
            }).ToArray();

            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        if (exception is ArgumentException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

            ProblemDetails problemDetails = new()
            {
                Type = ProblemTypes.EntidadeNaoProcessavel,
                Title = "Unprocessable entity",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = exception.Message,
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        // S40 §5: conflitos de estado de domínio (ConflitoDeEstadoException e demais
        // InvalidOperationException) → 409 ProblemDetails. PTAX já foi tratada acima.
        if (exception is InvalidOperationException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

            ProblemDetails problem = new()
            {
                Type = ProblemTypes.ConflitoDeEstado,
                Title = "Conflito de estado",
                Status = StatusCodes.Status409Conflict,
                Detail = exception.Message,
            };

            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        ProblemDetails internalProblemDetails = new()
        {
            Type = ProblemTypes.Interno,
            Title = "An unexpected error occurred",
            Status = StatusCodes.Status500InternalServerError,
        };

        await httpContext.Response.WriteAsJsonAsync(internalProblemDetails, cancellationToken);
        return true;
    }
}
