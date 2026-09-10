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
// │    · CORS (único punto de política de origen para todos los clientes)   │
// │    · Rate limiting por IP                                                │
// │    · Resilience: retry, circuit breaker, timeout por cluster             │
// │    · Correlation ID para trazabilidad                                    │
// │    · Error handling uniforme                                             │
// │                                                                          │
// │  Cada microservicio destino valida el token Bearer por su cuenta         │
// │  y construye su propio ActorContext a partir de los claims.              │
// │  Los microservicios NO deben configurar su propio CORS: el gateway es    │
// │  el borde de la red y responde el preflight antes de llegar a YARP.      │
// └─────────────────────────────────────────────────────────────────────────┘

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

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
// │  Error → CorrelationId → Cors → RateLimit → YARP                        │
// │                                                                          │
// │  UseCors() va ANTES de MapReverseProxy: el preflight OPTIONS se          │
// │  resuelve acá mismo y nunca llega a reenviarse al microservicio.         │
// │                                                                          │
// │  Sin UseAuthentication / UseAuthorization                                │
// │  El header Authorization (y la cookie, si la hay) se reenvía intacto     │
// │  al microservicio destino.                                               │
// └─────────────────────────────────────────────────────────────────────────┘

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors();
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