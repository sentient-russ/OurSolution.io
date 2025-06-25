using Microsoft.EntityFrameworkCore;
using os.Areas.Identity.Data;
using os.Services;
using os.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace os.Services
{
    public class VisitorRepository : IVisitorRepository
    {
        private readonly ApplicationDbContext _context;

        public VisitorRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public void TrackVisit(VisitorData visit)
        {
            _context.PageVisits.Add(new PageVisitModel
            {
                VisitorId = visit.VisitorId,
                IpAddress = visit.IpAddress,
                UserAgent = visit.UserAgent,
                Page = visit.Page,
                Referrer = visit.Referrer,
                Timestamp = visit.Timestamp
            });

            _context.SaveChanges();
        }

        public VisitorStats GetVisitorStats(DateTime startDate, DateTime endDate)
        {
            var visits = _context.PageVisits
                .Where(v => v.Timestamp >= startDate && v.Timestamp <= endDate)
                .ToList();

            return new VisitorStats
            {
                UniqueVisitors = visits.Select(v => v.VisitorId).Distinct().Count(),
                TotalPageViews = visits.Count,
                VisitsByPage = visits.GroupBy(v => v.Page)
                    .ToDictionary(g => g.Key, g => g.Count()),
                VisitsByReferrer = visits.Where(v => !string.IsNullOrEmpty(v.Referrer))
                    .GroupBy(v => v.Referrer)
                    .ToDictionary(g => g.Key, g => g.Count()),
                VisitsByDay = visits.GroupBy(v => v.Timestamp.Date)
                    .ToDictionary(g => g.Key, g => g.Count()),
                MonthlyUniqueVisitors = visits.GroupBy(v => new { Year = v.Timestamp.Year, Month = v.Timestamp.Month })
                    .ToDictionary(
                        g => $"{g.Key.Year}-{g.Key.Month:D2}",
                        g => g.Select(v => v.VisitorId).Distinct().Count()
                    )
            };
        }

        public Dictionary<int, Dictionary<int, int>> GetMonthlyUniqueVisitorsByYear(DateTime startDate, DateTime endDate)
        {
            // Initialize result dictionary
            var result = new Dictionary<int, Dictionary<int, int>>();

            // Query database for page visits within the date range
            // Only include visits to the home URL "/"
            var visits = _context.PageVisits
                .Where(v => v.Timestamp >= startDate &&
                           v.Timestamp <= endDate &&
                           v.Page == "/")
                .ToList();

            // Group visits by year and month, then count unique IP addresses (instead of visitor IDs)
            var visitorsByYearMonth = visits
                .GroupBy(v => new { Year = v.Timestamp.Year, Month = v.Timestamp.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    // Count unique IP addresses per month instead of visitor IDs
                    Count = g.Select(v => v.IpAddress).Distinct().Count()
                })
                .ToList();

            // Organize the data into the requested dictionary structure
            foreach (var item in visitorsByYearMonth)
            {
                if (!result.ContainsKey(item.Year))
                {
                    result[item.Year] = new Dictionary<int, int>();
                }

                result[item.Year][item.Month] = item.Count;
            }

            return result;
        }

        public List<int> GetAvailableYears()
        {
            // Get distinct years from the PageVisits table
            return _context.PageVisits
                .Select(v => v.Timestamp.Year)
                .Distinct()
                .OrderBy(y => y)
                .ToList();
        }
    }
}