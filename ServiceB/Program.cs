using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using Common;

var builder = WebApplication.CreateBuilder(args);

var shardMap = new Dictionary<string, IConnectionMultiplexer>
{
    ["shard-a"] = ConnectionMultiplexer.Connect("redis-a:6379"),
    ["shard-b"] = ConnectionMultiplexer.Connect("redis-b:6379")
};

builder.Services.AddSingleton(serviceProvider => shardMap);
builder.Services.AddSingleton(serviceProvider => new ConsistentHashRing<string>(shardMap.Keys));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId;
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceB"))
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
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceB"))
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

app.MapGet("/", (HttpContext context, ILogger<Program> logger) =>
{
    // var error = new
    // {
    //     status = 503,
    //     message = "Service B is temporarily unavailable. Please try again later."
    // };
    // logger.LogWarning("Service B is temporarily unavailable. Please try again later.");

    // return Results.Json(error, statusCode: 503);

    string containerName = Environment.MachineName;
    var port = context.Connection.LocalPort;
    return $"Hello from Service B on port {port} - {containerName}";
});

app.MapGet("/health", () => Results.Ok("B is Healthy"));

app.MapPost("/counter/increment/{id:int}", async (int id,
Dictionary<string, IConnectionMultiplexer> shards,
ConsistentHashRing<string> ring) =>
{
    var logger = app.Logger;    

    // var shardKey = id % 2 == 0 ? "even" : "odd";
    // var redis = shards[shardKey];
    // var db = redis.GetDatabase();

    var key = $"counter:{id}";
    var nodeName = ring.GetNode(key);
    var redis = shards[nodeName];
    var db = redis.GetDatabase();
    var endpoint = redis.GetEndPoints().FirstOrDefault();
    var serverInfo = endpoint?.ToString() ?? "unknown";

    var count = await db.StringIncrementAsync("counter");

    logger.LogInformation("📝 [WRITE] Counter {key} -> {count} via {node} - {server}", key, count, nodeName, serverInfo);

    return Results.Ok(new { key, counter = count, node = nodeName });
});

app.Run("http://+:80");
// app.Run();
