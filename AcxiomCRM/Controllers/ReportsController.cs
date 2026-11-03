using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(
            ApplicationDbContext context,
            ICrmScopeService scopeService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _scopeService = scopeService;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        // 1. Customer Report
        public async Task<IActionResult> CustomerReport(string? statusFilter, DateTime? startDate, DateTime? endDate)
        {
            var query = _scopeService.ApplyCustomerScope(_context.Customers.Include(c => c.Owner), User);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(c => c.Status == statusFilter);
            }

            if (startDate.HasValue)
            {
                query = query.Where(c => c.CreatedDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(c => c.CreatedDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));
            }

            var vm = new CustomerReportViewModel
            {
                StatusFilter = statusFilter,
                StartDate = startDate,
                EndDate = endDate,
                Customers = await query.OrderByDescending(c => c.CreatedDate).ToListAsync()
            };

            return View(vm);
        }

        // 2. Lead Report
        public async Task<IActionResult> LeadReport(string? sourceFilter, string? statusFilter)
        {
            var query = _scopeService.ApplyLeadScope(_context.Leads.Include(l => l.AssignedUser), User);

            if (!string.IsNullOrWhiteSpace(sourceFilter))
            {
                query = query.Where(l => l.Source == sourceFilter);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(l => l.Status == statusFilter);
            }

            var list = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
            var total = await query.CountAsync();
            var converted = await query.CountAsync(l => l.Status == "Converted");

            var vm = new LeadReportViewModel
            {
                SourceFilter = sourceFilter,
                StatusFilter = statusFilter,
                TotalLeads = total,
                ConvertedLeads = converted,
                Leads = list
            };

            return View(vm);
        }

        // 3. Follow-Up Report
        public async Task<IActionResult> FollowUpReport(string? statusFilter)
        {
            var query = _scopeService.ApplyFollowUpScope(
                _context.FollowUps
                    .Include(f => f.Customer)
                    .Include(f => f.Lead)
                    .Include(f => f.AssignedUser),
                User);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(f => f.Status == statusFilter);
            }

            var list = await query.OrderBy(f => f.FollowUpDate).ToListAsync();
            var planned = await query.CountAsync(f => f.Status == "Planned");
            var completed = await query.CountAsync(f => f.Status == "Completed");
            var missed = await query.CountAsync(f => f.Status == "Missed");
            var overdue = await query.CountAsync(f => f.Status == "Planned" && f.FollowUpDate.Date < DateTime.Today);

            var vm = new FollowUpReportViewModel
            {
                StatusFilter = statusFilter,
                TotalPlanned = planned,
                TotalCompleted = completed,
                TotalMissed = missed,
                TotalOverdue = overdue,
                FollowUps = list
            };

            return View(vm);
        }

        // 4. Opportunity Report
        public async Task<IActionResult> OpportunityReport(string? stageFilter)
        {
            var query = _scopeService.ApplyOpportunityScope(
                _context.Opportunities
                    .Include(o => o.Customer)
                    .Include(o => o.AssignedUser),
                User);

            if (!string.IsNullOrWhiteSpace(stageFilter))
            {
                query = query.Where(o => o.Stage == stageFilter);
            }

            var list = await query.OrderByDescending(o => o.Amount).ToListAsync();
            var totalAmount = list.Sum(o => o.Amount);
            var totalWeighted = Math.Round(list.Sum(o => o.WeightedAmount), 2);

            var vm = new OpportunityReportViewModel
            {
                StageFilter = stageFilter,
                TotalAmount = totalAmount,
                TotalWeightedAmount = totalWeighted,
                Opportunities = list
            };

            return View(vm);
        }

        // 5. Pipeline Report (Manager / Admin / Scoped Sales view)
        public async Task<IActionResult> PipelineReport()
        {
            var query = _scopeService.ApplyOpportunityScope(_context.Opportunities.Include(o => o.AssignedUser), User);
            var allOpps = await query.ToListAsync();

            var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var stageSummaries = stages.Select(stage =>
            {
                var matching = allOpps.Where(o => o.Stage == stage).ToList();
                return new StageSummaryItem
                {
                    Stage = stage,
                    Count = matching.Count,
                    TotalAmount = matching.Sum(m => m.Amount),
                    WeightedAmount = Math.Round(matching.Sum(m => m.WeightedAmount), 2)
                };
            }).ToList();

            var ownerSummaries = allOpps
                .GroupBy(o => o.AssignedUser != null ? o.AssignedUser.FullName : "Unassigned")
                .Select(g => new OwnerSummaryItem
                {
                    OwnerName = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    WeightedAmount = Math.Round(g.Sum(x => x.WeightedAmount), 2)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            var vm = new PipelineReportViewModel
            {
                StageSummaries = stageSummaries,
                OwnerSummaries = ownerSummaries,
                GrandTotalPipeline = allOpps.Where(o => o.Stage != "Lost").Sum(o => o.Amount),
                GrandTotalWeighted = Math.Round(allOpps.Where(o => o.Stage != "Lost").Sum(o => o.WeightedAmount), 2)
            };

            return View(vm);
        }

        // 6. User Activity Report (Admin / Manager)
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> UserActivityReport()
        {
            var users = await _userManager.Users.Where(u => u.IsActive).ToListAsync();
            var activityItems = new List<UserActivityItem>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var logins = await _context.AuditLogs.CountAsync(a => a.UserId == u.Id && a.Action == "Login");
                var creates = await _context.AuditLogs.CountAsync(a => a.UserId == u.Id && a.Action == "Create");
                var updates = await _context.AuditLogs.CountAsync(a => a.UserId == u.Id && a.Action == "Update");
                var completedActs = await _context.Activities.CountAsync(a => a.AssignedTo == u.Id && a.Status == "Completed");

                activityItems.Add(new UserActivityItem
                {
                    UserName = u.FullName,
                    Role = roles.FirstOrDefault() ?? "None",
                    LoginsCount = logins,
                    CreatedRecordsCount = creates,
                    UpdatedRecordsCount = updates,
                    CompletedActivitiesCount = completedActs
                });
            }

            var vm = new UserActivityReportViewModel
            {
                UserActivities = activityItems.OrderByDescending(a => a.CreatedRecordsCount).ToList()
            };

            return View(vm);
        }

        // 7. Sales & Conversion Report (Section 4.11 & 11)
        public async Task<IActionResult> SalesConversionReport()
        {
            var leadsQuery = _scopeService.ApplyLeadScope(_context.Leads.Include(l => l.AssignedUser), User);
            var oppsQuery = _scopeService.ApplyOpportunityScope(_context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser), User);

            var totalLeads = await leadsQuery.CountAsync();
            var convertedLeads = await leadsQuery.CountAsync(l => l.Status == "Converted");
            var convertedList = await leadsQuery.Where(l => l.Status == "Converted").OrderByDescending(l => l.CreatedDate).ToListAsync();

            var closedOpps = await oppsQuery.Where(o => o.Stage == "Won" || o.Stage == "Lost").ToListAsync();
            var wonOpps = closedOpps.Where(o => o.Stage == "Won").ToList();
            var lostOpps = closedOpps.Where(o => o.Stage == "Lost").ToList();

            var vm = new SalesConversionReportViewModel
            {
                TotalLeads = totalLeads,
                ConvertedLeads = convertedLeads,
                TotalDeals = closedOpps.Count,
                WonDeals = wonOpps.Count,
                LostDeals = lostOpps.Count,
                TotalWonRevenue = wonOpps.Sum(o => o.Amount),
                TotalLostRevenue = lostOpps.Sum(o => o.Amount),
                ConvertedLeadsList = convertedList,
                ClosedOpportunitiesList = closedOpps.OrderByDescending(o => o.CreatedDate).ToList()
            };

            return View(vm);
        }

        // 8. Security & Business Audit Report (Section 4.11 & 11)
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> AuditReport(string? moduleFilter, string? actionFilter)
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(moduleFilter))
            {
                query = query.Where(a => a.EntityName == moduleFilter);
            }

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(a => a.Action == actionFilter);
            }

            var logs = await query.OrderByDescending(a => a.CreatedDate).Take(100).ToListAsync();

            var totalEvents = await _context.AuditLogs.CountAsync();
            var logins = await _context.AuditLogs.CountAsync(a => a.Action == "Login");
            var failedLogins = await _context.AuditLogs.CountAsync(a => a.Action == "Failed Login");
            var security = await _context.AuditLogs.CountAsync(a => a.Action == "Security" || a.Action == "Role Change");
            var crud = await _context.AuditLogs.CountAsync(a => a.Action == "Create" || a.Action == "Update" || a.Action == "Delete");

            var vm = new AuditReportViewModel
            {
                TotalAuditEvents = totalEvents,
                SuccessfulLogins = logins,
                FailedLogins = failedLogins,
                RoleAndSecurityChanges = security,
                CrudOperations = crud,
                AuditLogs = logs
            };

            return View(vm);
        }
    }
}
