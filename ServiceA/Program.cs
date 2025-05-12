using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddHttpClient("ServiceB", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

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
    logger.LogInformation("andled {Method} {Path} in {Elapsed} ms",
        context.Request.Method,
        context.Request.Path,
        sw.ElapsedMilliseconds);
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
    try
    {    
        var httpClient = httpClientFactory.CreateClient("ServiceB");            
        var response = await httpClient.GetAsync("/");
        
        logger.LogWarning("called B ");
        
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