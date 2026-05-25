namespace Zenthia.Api.Gateway.Middleware;

/// <summary>
/// Asegura que cada request tenga un X-Correlation-Id único.
/// Si el cliente no lo envía, se genera uno nuevo.
/// Se propaga al microservicio destino y se devuelve en la respuesta.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        context.Items[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
