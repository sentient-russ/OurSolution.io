namespace os.Services
{
    public class StatsTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly StatsTracker _statsTracker;

        public StatsTrackingMiddleware(RequestDelegate next, StatsTracker statsTracker)
        {
            _next = next;
            _statsTracker = statsTracker;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            _statsTracker.TrackRequest();
            await _next(context);
        }
    }

}
