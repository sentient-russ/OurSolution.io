using os.Services;

public class StatsTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;

    public StatsTrackingMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
    {
        _next = next;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get the StatsTracker service from the scoped services
        var statsTracker = context.RequestServices.GetRequiredService<StatsTracker>();

        // Track the request
        statsTracker.TrackRequest();

        // Call the next middleware in the pipeline
        await _next(context);
    }
}
