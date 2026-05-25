using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Yarp.ReverseProxy.Forwarder;

namespace Zenthia.Api.Gateway.Extensions;

public static class ResilienceExtensions
{
    /// <summary>
    /// Pipeline de resiliencia aplicado a cada cluster de YARP:
    ///
    ///   1. Timeout       → falla si el micro destino tarda más de 10 s
    ///   2. Retry         → 3 reintentos con backoff exponencial (1s, 2s, 4s)
    ///   3. CircuitBreaker → abre si el 50% de las últimas 10 requests fallan,
    ///                       permanece abierto 60 s y retorna 503 inmediato
    /// </summary>
    public static IServiceCollection AddZenthiaResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registrar cliente resiliente por cada cluster
        foreach (var cluster in GetClusterNames())
        {
            services.AddHttpClient(cluster, client =>
            {
                // Timeout base del cliente — el pipeline de Polly
                // agrega su propio timeout por encima de este
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddResilienceHandler($"{cluster}-pipeline", pipeline =>
            {
                pipeline.AddTimeout(TimeSpan.FromSeconds(10));

                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = static args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException ||
                        args.Outcome.Result?.StatusCode is
                            >= HttpStatusCode.InternalServerError or
                            HttpStatusCode.RequestTimeout)
                });

                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(60)
                });
            });
        }

        services.AddSingleton<IForwarderHttpClientFactory,
                              ResilientForwarderHttpClientFactory>();

        return services;
    }

    private static IEnumerable<string> GetClusterNames() =>
    [
        "cluster-security",
        "cluster-reference",
        "cluster-company",
        "cluster-employee",
        "cluster-agreement",
        "cluster-payroll",
        "cluster-payslip",
        "cluster-lsd"
    ];
}

// ── YARP HTTP client factory con pipeline de resiliencia ─────────────────

// ✅ Correcto — IHttpMessageHandlerFactory devuelve el handler
// con el pipeline de Polly ya incluido, sin el wrapper HttpClient
internal sealed class ResilientForwarderHttpClientFactory(
    IHttpMessageHandlerFactory handlerFactory)
    : IForwarderHttpClientFactory
{
    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        var handler = handlerFactory.CreateHandler(context.ClusterId);
        return new HttpMessageInvoker(handler, disposeHandler: false);
    }
}
