namespace ServiceA;

public static class HttpClientTracingExtensions
{
    public static void AddTracingHeaders(this HttpRequestMessage request)
    {
        var activity = System.Diagnostics.Activity.Current;
        if (activity != null)
        {
            request.Headers.TryAddWithoutValidation("traceparent", $"00-{activity.TraceId}-{activity.SpanId}-01");
        }
    }
}