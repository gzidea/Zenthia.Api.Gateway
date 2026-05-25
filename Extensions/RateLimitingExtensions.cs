using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Zenthia.Api.Gateway.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>
    /// Rate limiting por IP del cliente.
    ///
    /// El gateway no valida el JWT, por lo que no puede usar el claim "sub"
    /// como partition key. Se usa la IP como identificador del cliente.
    ///
    /// Políticas:
    ///   per-client      → límite general: 100 req/min por IP
    ///   payroll-throttle → payroll es costoso: 3 ejecuciones/hora por IP
    ///   catalogues-burst → catálogos son lecturas baratas: token bucket generoso
    /// </summary>
    public static IServiceCollection AddZenthiaRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // ── General: por IP ───────────────────────────────────────────
            options.AddPolicy("per-client", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit          = 100,
                        Window               = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit           = 10
                    }));

            // ── Payroll: por IP, muy restrictivo ──────────────────────────
            options.AddPolicy("payroll-throttle", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window      = TimeSpan.FromHours(1),
                        QueueLimit  = 0
                    }));

            // ── Catálogos: token bucket generoso ──────────────────────────
            options.AddPolicy("catalogues-burst", _ =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: "global-catalogues",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit          = 500,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod     = 500,
                        AutoReplenishment   = true,
                        QueueLimit          = 20
                    }));

            // ── Respuesta 429 uniforme ────────────────────────────────────
            options.OnRejected = async (ctx, cancellationToken) =>
            {
                ctx.HttpContext.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.ContentType = "application/problem+json";

                var retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? (int)retry.TotalSeconds : 60;

                ctx.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type              = "https://httpstatuses.com/429",
                    title             = "Too many requests",
                    status            = 429,
                    retryAfterSeconds = retryAfter,
                    correlationId     = ctx.HttpContext.Items[
                        Middleware.CorrelationIdMiddleware.HeaderName]?.ToString()
                }, cancellationToken: cancellationToken);
            };
        });

        return services;
    }

    // ── IP del cliente, considera X-Forwarded-For si viene de un proxy ────

    private static string GetClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
