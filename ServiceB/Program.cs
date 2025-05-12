var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId;
});

var app = builder.Build();

app.UseRouting();

app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();
    var logger = app.Logger;
    logger.LogInformation("Handled {Method} {Path} in {Elapsed} ms",
        context.Request.Method,
        context.Request.Path,
        sw.ElapsedMilliseconds);
});

app.MapGet("/", (HttpContext context, ILogger<Program> logger) =>
{
    var error = new
    {
        status = 503,
        message = "Service B is temporarily unavailable. Please try again later."
    };
    logger.LogWarning("Service B is temporarily unavailable. Please try again later.");


    return Results.Json(error, statusCode: 503);
    
    // string containerName = Environment.MachineName;
    // var port = context.Connection.LocalPort;
    // return $"Hello from Service B on port {port} - {containerName}";
});

app.MapGet("/health", () => Results.Ok("B is Healthy"));

// app.Run("http://+:80");
app.Run();
