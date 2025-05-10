var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

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

app.MapGet("/", (HttpContext context) =>
{
    var port = context.Connection.LocalPort;
    return $"Hello from Service B on port {port}";
});

app.MapGet("/health", () => Results.Ok("B is Healthy"));

app.Run("http://+:80");
