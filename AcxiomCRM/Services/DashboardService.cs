using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;

        public DashboardService(ApplicationDbContext context, ICrmScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(ClaimsPrincipal user, string filter, DateTime? startDate = null, DateTime? endDate = null)
        {
            var vm = new DashboardViewModel
            {
                Filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.ToLower(),
                StartDate = startDate,
                EndDate = endDate
            };

            var now = DateTime.UtcNow;
            DateTime? filterStart = null;
            DateTime? filterEnd = null;

            switch (vm.Filter)
            {
                case "today":
                    filterStart = DateTime.Today;
                    filterEnd = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "thisweek":
                    var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    filterStart = startOfWeek;
                    filterEnd = startOfWeek.AddDays(7).AddTicks(-1);
                    break;
                case "thismonth":
                    filterStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    filterEnd = filterStart.Value.AddMonths(1).AddTicks(-1);
                    break;
                case "custom":
                    filterStart = startDate;
                    filterEnd = endDate;
                    break;
                default:
                    // "all"
                    break;
            }

            // Scoped base queries
            var customersQuery = _scopeService.ApplyCustomerScope(_context.Customers.AsNoTracking(), user);
            var leadsQuery = _scopeService.ApplyLeadScope(_context.Leads.AsNoTracking(), user);
            var oppsQuery = _scopeService.ApplyOpportunityScope(_context.Opportunities.AsNoTracking(), user);
            var followUpsQuery = _scopeService.ApplyFollowUpScope(_context.FollowUps.AsNoTracking(), user);
            var activitiesQuery = _scopeService.ApplyActivityScope(_context.Activities.AsNoTracking(), user);

            if (filterStart.HasValue)
            {
                customersQuery = customersQuery.Where(c => c.CreatedDate >= filterStart.Value);
                leadsQuery = leadsQuery.Where(l => l.CreatedDate >= filterStart.Value);
                oppsQuery = oppsQuery.Where(o => o.CreatedDate >= filterStart.Value);
            }
            if (filterEnd.HasValue)
            {
                customersQuery = customersQuery.Where(c => c.CreatedDate <= filterEnd.Value);
                leadsQuery = leadsQuery.Where(l => l.CreatedDate <= filterEnd.Value);
                oppsQuery = oppsQuery.Where(o => o.CreatedDate <= filterEnd.Value);
            }

            // KPI Counts
            vm.TotalCustomers = await customersQuery.CountAsync();
            vm.TotalLeads = await leadsQuery.CountAsync();
            vm.OpenLeads = await leadsQuery.CountAsync(l => l.Status != "Converted" && l.Status != "Lost");

            // Opportunities calculation using in-memory aggregation (avoids SQLite decimal Sum translation limitation)
            var allOpps = await oppsQuery.ToListAsync();

            vm.TotalOpportunities = allOpps.Count;
            vm.OpenOpportunities = allOpps.Count(o => o.Status == "Open" || (o.Stage != "Won" && o.Stage != "Lost"));
            vm.WonOpportunities = allOpps.Count(o => o.Stage == "Won");
            vm.LostOpportunities = allOpps.Count(o => o.Stage == "Lost");

            var openOpps = allOpps.Where(o => o.Status == "Open" || (o.Stage != "Won" && o.Stage != "Lost")).ToList();
            vm.TotalPipelineValue = openOpps.Sum(o => o.Amount);
            vm.WeightedPipelineValue = Math.Round(openOpps.Sum(o => o.WeightedAmount), 2);

            vm.PendingFollowUps = await followUpsQuery.CountAsync(f => f.Status == "Planned");

            // Chart 1: Lead Status
            var leadStatuses = new[] { "New", "Contacted", "Qualified", "Lost", "Converted" };
            var allLeadStatuses = await leadsQuery.Select(l => l.Status).ToListAsync();
            var leadGrouped = allLeadStatuses
                .GroupBy(s => s)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var status in leadStatuses)
            {
                vm.LeadStatusLabels.Add(status);
                vm.LeadStatusCounts.Add(leadGrouped.TryGetValue(status, out var count) ? count : 0);
            }

            // Chart 2: Opportunity Pipeline by Stage
            var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var oppGrouped = allOpps
                .GroupBy(o => o.Stage)
                .ToDictionary(g => g.Key, g => new { Amount = g.Sum(x => x.Amount), Count = g.Count() });

            foreach (var stage in stages)
            {
                vm.PipelineStageLabels.Add(stage);
                if (oppGrouped.TryGetValue(stage, out var data))
                {
                    vm.PipelineStageAmounts.Add(data.Amount);
                    vm.PipelineStageCounts.Add(data.Count);
                }
                else
                {
                    vm.PipelineStageAmounts.Add(0);
                    vm.PipelineStageCounts.Add(0);
                }
            }

            // Chart 3: Monthly Sales / Win Performance (Past 6 Months)
            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = currentMonth.AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                var monthLabel = monthStart.ToString("MMM yyyy");

                var wonSum = allOpps
                    .Where(o => o.Stage == "Won" && o.CreatedDate >= monthStart && o.CreatedDate <= monthEnd)
                    .Sum(o => o.Amount);

                vm.MonthlySalesLabels.Add(monthLabel);
                vm.MonthlySalesAmounts.Add(wonSum);
            }

            // Recent Activities
            var recentActs = await activitiesQuery
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .Select(a => new RecentActivityItem
                {
                    Type = a.ActivityType,
                    Subject = a.Subject,
                    Date = a.ActivityDate,
                    Status = a.Status,
                    RelatedName = a.Customer != null ? a.Customer.CustomerName : (a.Lead != null ? a.Lead.LeadName : null)
                })
                .ToListAsync();
            vm.RecentActivities = recentActs;

            // Upcoming / Overdue Follow-ups
            var upcoming = await followUpsQuery
                .Where(f => f.Status == "Planned")
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .Select(f => new UpcomingFollowUpItem
                {
                    Id = f.FollowUpId,
                    Subject = f.Subject,
                    Date = f.FollowUpDate,
                    Type = f.FollowUpType,
                    RelatedName = f.Customer != null ? f.Customer.CustomerName : (f.Lead != null ? f.Lead.LeadName : null),
                    IsOverdue = f.FollowUpDate.Date < DateTime.Today
                })
                .ToListAsync();
            vm.UpcomingFollowUps = upcoming;

            return vm;
        }
    }
}
