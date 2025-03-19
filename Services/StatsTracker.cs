using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;

namespace os.Services
{
    public class StatsTracker
    {
        private readonly ConcurrentDictionary<string, DateTime> _users = new();
        private readonly IHttpContextAccessor _httpContextAccessor;

        public StatsTracker(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        [Authorize(Roles = "Administrator")]
        public void TrackRequest()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Get correct client IP through proxy
            var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? context.Connection.RemoteIpAddress?.ToString();

            if (!string.IsNullOrEmpty(ipAddress))
            {
                _users.AddOrUpdate(ipAddress, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
            }
        }
        [Authorize(Roles = "Administrator")]
        public Dictionary<string, DateTime> GetActiveUsers(TimeSpan timeout)
        {
            var cutoff = DateTime.UtcNow - timeout;
            return _users.Where(x => x.Value > cutoff)
                       .OrderByDescending(x => x.Value)
                       .ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
