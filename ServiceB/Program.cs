using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("redis-primary:6379")
);

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

app.MapPost("/counter/increment", async (IConnectionMultiplexer redis) =>
{
    var logger = app.Logger;

    var db = redis.GetDatabase();
    var endpoint = redis.GetEndPoints().FirstOrDefault();
    var serverInfo = endpoint?.ToString() ?? "unknown";

    var count = await db.StringIncrementAsync("counter");

    logger.LogInformation("🔸 [WRITE] Counter incremented to {count} via {server}", count, serverInfo);
    
    return Results.Ok(new { counter = count });
});

app.Run("http://+:80");
// app.Run();
