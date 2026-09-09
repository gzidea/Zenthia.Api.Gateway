using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Health;
using Zenthia.Api.Gateway.Extensions;
using Zenthia.Api.Gateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ┌─────────────────────────────────────────────────────────────────────────┐
// │  Services                                                                │
// │                                                                          │
// │  El gateway NO valida JWT. Responsabilidades:                            │
// │    · Enrutamiento (YARP)                                                 │
// │    · Rate limiting por IP                                                │
// │    · Resilience: retry, circuit breaker, timeout por cluster             │
// │    · Correlation ID para trazabilidad                                    │
// │    · Error handling uniforme                                             │
// │                                                                          │
// │  Cada microservicio destino valida el token Bearer por su cuenta         │
// │  y construye su propio ActorContext a partir de los claims.              │
// └─────────────────────────────────────────────────────────────────────────┘

builder.Services
    .AddZenthiaRateLimiting()
    .AddZenthiaResilience(builder.Configuration);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

builder.Services.Configure<ConsecutiveFailuresHealthPolicyOptions>(options =>
{
    options.DefaultThreshold = 3; // 3 fallas seguidas = destino down
});

// ┌─────────────────────────────────────────────────────────────────────────┐
// │  Pipeline                                                                │
// │                                                                          │
// │  Error → CorrelationId → RateLimit → YARP                               │
// │                                                                          │
// │  Sin UseAuthentication / UseAuthorization                                │
// │  El header Authorization se reenvía intacto al microservicio destino    │
// └─────────────────────────────────────────────────────────────────────────┘

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.MapGet("/gateway/health-status", (IProxyStateLookup proxyState) =>
{
    var result = new List<object>();

    foreach (var cluster in proxyState.GetClusters())
    {
        foreach (var destination in cluster.Destinations.Values)
        {
            result.Add(new
            {
                Cluster = cluster.ClusterId,
                Destination = destination.DestinationId,
                Address = destination.Model.Config.Address,
                Health = destination.Health.Active.ToString(),
                Passive = destination.Health.Passive.ToString()
            });
        }
    }

    return Results.Ok(result);
});

app.Run();
