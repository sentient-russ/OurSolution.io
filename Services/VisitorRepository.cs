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
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
    }
}