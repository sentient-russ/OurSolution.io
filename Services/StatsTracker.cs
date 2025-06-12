using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace os.Services
{
    public class StatsTracker
    {
        private readonly ConcurrentDictionary<string, DateTime> _users = new();
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IVisitorRepository _visitorRepository;
        private const string VisitorCookieName = "visitor_id";
        private const int CookieExpirationDays = 365;

        public StatsTracker(IHttpContextAccessor httpContextAccessor, IVisitorRepository visitorRepository = null)
        {
            _httpContextAccessor = httpContextAccessor;
            _visitorRepository = visitorRepository;
        }

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

            // Track detailed visitor data if repository is available
            TrackVisitorData(context, ipAddress);
        }

        public Dictionary<string, DateTime> GetActiveUsers(TimeSpan timeout)
        {
            var cutoff = DateTime.UtcNow - timeout;
            return _users.Where(x => x.Value > cutoff)
                       .OrderByDescending(x => x.Value)
                       .ToDictionary(x => x.Key, x => x.Value);
        }

        // New methods for enhanced visitor tracking
        private void TrackVisitorData(HttpContext context, string ipAddress)
        {
            if (_visitorRepository == null) return;

            // Get or create visitor ID
            var visitorId = GetOrCreateVisitorId(context);

            // Get client info
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var page = context.Request.Path.ToString();
            var referrer = context.Request.Headers["Referer"].ToString();

            // Save visit to repository
            _visitorRepository.TrackVisit(new VisitorData
            {
                VisitorId = visitorId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Page = page,
                Referrer = referrer,
                Timestamp = DateTime.UtcNow
            });
        }

        private string GetOrCreateVisitorId(HttpContext context)
        {
            // Try to get existing visitor ID from cookie
            if (context.Request.Cookies.TryGetValue(VisitorCookieName, out var existingId) && !string.IsNullOrEmpty(existingId))
            {
                return existingId;
            }

            // Create new visitor ID
            var newId = Guid.NewGuid().ToString();

            // Set cookie with long expiration for returning visitor tracking
            context.Response.Cookies.Append(VisitorCookieName, newId, new CookieOptions
            {
                Expires = DateTime.UtcNow.AddDays(CookieExpirationDays),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true // Consider GDPR implications
            });

            return newId;
        }

        [Authorize(Roles = "Administrator")]
        public VisitorStats GetVisitorStats(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (_visitorRepository == null)
            {
                return new VisitorStats
                {
                    UniqueVisitors = 0,
                    TotalPageViews = 0,
                    VisitsByPage = new Dictionary<string, int>(),
                    VisitsByReferrer = new Dictionary<string, int>(),
                    VisitsByDay = new Dictionary<DateTime, int>()
                };
            }

            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            return _visitorRepository.GetVisitorStats(startDate.Value, endDate.Value);
        }
    }

    // Interfaces and supporting classes

    public interface IVisitorRepository
    {
        void TrackVisit(VisitorData visit);
        VisitorStats GetVisitorStats(DateTime startDate, DateTime endDate);
    }

    public class VisitorData
    {
        public string VisitorId { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Page { get; set; }
        public string Referrer { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class VisitorStats
    {
        public int UniqueVisitors { get; set; }
        public int TotalPageViews { get; set; }
        public Dictionary<string, int> VisitsByPage { get; set; }
        public Dictionary<string, int> VisitsByReferrer { get; set; }
        public Dictionary<DateTime, int> VisitsByDay { get; set; }
    }
}