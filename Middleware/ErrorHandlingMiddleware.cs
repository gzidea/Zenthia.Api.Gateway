using System.Net;
using System.Text.Json;

namespace Zenthia.Api.Gateway.Middleware;

/// <summary>
/// Captura cualquier excepción no manejada del pipeline y retorna
/// un JSON uniforme sin exponer detalles internos al cliente.
/// </summary>
public sealed class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
                                ?? "unknown";

            logger.LogError(ex,
                "Unhandled exception — CorrelationId: {CorrelationId} | {Method} {Path}",
                correlationId, context.Request.Method, context.Request.Path);

            await WriteErrorAsync(context, ex, correlationId);
        }
    }

    private static Task WriteErrorAsync(
        HttpContext context,
        Exception   exception,
        string      correlationId)
    {
        var (statusCode, title) = exception switch
        {
            OperationCanceledException => (HttpStatusCode.GatewayTimeout,        "Gateway timeout"),
            ArgumentException          => (HttpStatusCode.BadRequest,            "Bad request"),
            _                          => (HttpStatusCode.InternalServerError,    "An unexpected error occurred")
        };

        context.Response.StatusCode  = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type          = $"https://httpstatuses.com/{(int)statusCode}",
            title,
            status        = (int)statusCode,
            correlationId,
            traceId       = context.TraceIdentifier
        }, JsonOptions));
    }
}
