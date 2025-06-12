using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using os.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace os.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class StatsController : Controller
    {
        private readonly StatsTracker _statsTracker;

        public StatsController(StatsTracker statsTracker)
        {
            _statsTracker = statsTracker;
        }

        public IActionResult Index(List<int> years = null)
        {
            // Get active users (existing functionality)
            var activeUsers = _statsTracker.GetActiveUsers(TimeSpan.FromMinutes(5));
            ViewBag.UserCount = activeUsers.Count;

            // Get available years for the chart
            var availableYears = _statsTracker.GetAvailableYears();
            ViewBag.AvailableYears = availableYears;

            // If no years selected, default to current year
            var selectedYears = years?.Count > 0 ? years : new List<int> { DateTime.UtcNow.Year };
            ViewBag.SelectedYears = selectedYears;

            // Get visitor data by month and year
            var allMonthlyData = _statsTracker.GetMonthlyUniqueVisitorsByYear();

            // Filter by selected years
            var filteredData = allMonthlyData
                .Where(y => selectedYears.Contains(y.Key))
                .ToDictionary(y => y.Key, y => y.Value);

            // Add the monthly visitor data to ViewBag for the chart
            ViewBag.MonthlyVisitorData = filteredData;

            // Return the active users model (existing functionality)
            return View(activeUsers);
        }
    }
}