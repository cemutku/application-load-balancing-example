using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using ServiceA;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(serviceProvider =>
{
    var shardA = ConnectionMultiplexer.Connect("redis-a-replica:6379");
    var shardB = ConnectionMultiplexer.Connect("redis-b-replica:6379");

    return new Dictionary<string, IConnectionMultiplexer>
    {
        ["even"] = shardA,
        ["odd"] = shardB
    };
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddHttpClient("ServiceB", client =>
{
    client.BaseAddress = new Uri("http://serviceb");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId;
});

builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
});

var activitySource = new ActivitySource("ServiceA.Activity");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceA"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri("http://jaeger:4317");
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceA"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter();
    });

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);      // Main API
    options.ListenAnyIP(9184);    // Metrics
});

var app = builder.Build();

app.MapPrometheusScrapingEndpoint("/metrics");

app.UseRouting();

app.Use(async (context, next) =>
{
    var logger = app.Logger;
    var containerName = Environment.MachineName;
    var sw = System.Diagnostics.Stopwatch.StartNew();

    await next();

    sw.Stop();

    var activity = System.Diagnostics.Activity.Current;
    var traceId = activity?.TraceId.ToString() ?? context.TraceIdentifier;

    logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms | Container={Container} TraceId={TraceId}",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        sw.ElapsedMilliseconds,
        containerName,
        traceId);
});

app.MapGet("/", (HttpContext context) =>
{
    string containerName = Environment.MachineName;
    var port = context.Connection.LocalPort;
    return $"Hello from Service A on port {port} - {containerName}";
});

app.MapGet("/serviceb-hello", async (
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    using var activity = activitySource.StartActivity("Call ServiceB");

    activity?.SetTag("caller.service", "ServiceA");
    activity?.SetTag("target.service", "ServiceB");
    activity?.SetTag("custom.trace", "true");

    try
    {    
        var httpClient = httpClientFactory.CreateClient("ServiceB");        
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.AddTracingHeaders();
        var response = await httpClient.SendAsync(request);
                
        response.EnsureSuccessStatusCode();
        return Results.Text(await response.Content.ReadAsStringAsync());
    }
    catch (BrokenCircuitException)
    {
        logger.LogWarning("Circuit breaker is open — skipping call to ServiceB");
        var error = new
        {
            status = 503,
            message = "Service is temporarily unavailable. Please try again later."
        };

        return Results.Json(error, statusCode: 503);
    }
    catch (System.Exception ex)
    {
        logger.LogError(ex, "Unhandled error when calling ServiceB");
        throw;
    }
});

app.MapGet("/health", () => Results.Ok("A is Healthy"));

app.MapGet("/counter/{id:int}", async (int id, Dictionary<string, IConnectionMultiplexer> shards) =>    
{
    var logger = app.Logger;

    var shardKey = id % 2 == 0 ? "even" : "odd";
    var redis = shards[shardKey];

    var db = redis.GetDatabase();
    var endpoint = redis.GetEndPoints().FirstOrDefault();
    var serverInfo = endpoint?.ToString() ?? "unknown";

    var key = $"counter:{id}";
    var count = await db.StringGetAsync("counter");
    
    logger.LogInformation("🔍 [READ] Counter {key} = {count} from {shard} - {server}", key, count, shardKey, serverInfo);

    return Results.Ok(new { key, counter = (int)count, shard = shardKey });
});

app.Run("http://+:80");

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 2,
            sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(300 * attempt), // exponential backoff
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"Retry {retryAttempt} after {timespan.TotalMilliseconds}ms due to {outcome.Exception?.Message}");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,         // break after 3 consecutive failures
            durationOfBreak: TimeSpan.FromSeconds(15),     // stay open for 15 seconds
            onBreak: (outcome, timespan) =>
            {
                Console.WriteLine($"Circuit broken for {timespan.TotalSeconds}s due to: {outcome.Exception?.Message}");
            },
            onReset: () =>
            {
                Console.WriteLine("Circuit reset.");
            },
            onHalfOpen: () =>
            {
                Console.WriteLine("Circuit is half-open: testing...");
            });
}