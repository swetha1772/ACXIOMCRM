using System;
using System.Linq;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ActivitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly string[] AllowedTypes = { "Call", "Meeting", "Email", "Task" };

        public ActivitiesController(
            ApplicationDbContext context,
            ICrmScopeService scopeService,
            IAuditService auditService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _scopeService = scopeService;
            _auditService = auditService;
            _userManager = userManager;
        }

        // GET: Activities
        public async Task<IActionResult> Index(string? typeFilter, string? statusFilter, DateTime? dateFilter, int page = 1)
        {
            var query = _scopeService.ApplyActivityScope(
                _context.Activities
                    .Include(a => a.Customer)
                    .Include(a => a.Lead)
                    .Include(a => a.AssignedUser),
                User);

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                query = query.Where(a => a.ActivityType == typeFilter);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(a => a.Status == statusFilter);
            }

            if (dateFilter.HasValue)
            {
                var target = dateFilter.Value.Date;
                query = query.Where(a => a.ActivityDate.Date == target);
            }

            var totalItems = await query.CountAsync();
            var pageSize = 10;
            var activities = await query
                .OrderByDescending(a => a.ActivityDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new ActivityListViewModel
            {
                TypeFilter = typeFilter,
                StatusFilter = statusFilter,
                DateFilter = dateFilter,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Activities = activities
            };

            return View(vm);
        }

        // GET: Activities/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var activity = await _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.AssignedUser)
                .FirstOrDefaultAsync(m => m.ActivityId == id);

            if (activity == null) return NotFound();

            if (!_scopeService.CanAccessActivity(activity, User))
            {
                return Forbid();
            }

            return View(activity);
        }

        // GET: Activities/Create
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            var vm = new ActivityCreateEditViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                ActivityType = "Call",
                Status = "Completed",
                ActivityDate = DateTime.UtcNow,
                CustomersList = await GetCustomersSelectListAsync(customerId),
                LeadsList = await GetLeadsSelectListAsync(leadId),
                UsersList = await GetUsersSelectListAsync()
            };

            return View(vm);
        }

        // POST: Activities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ActivityCreateEditViewModel model)
        {
            if (!AllowedTypes.Contains(model.ActivityType))
            {
                ModelState.AddModelError("ActivityType", $"Activity Type must be one of: {string.Join(", ", AllowedTypes)}");
            }

            if (!ModelState.IsValid)
            {
                model.CustomersList = await GetCustomersSelectListAsync(model.CustomerId);
                model.LeadsList = await GetLeadsSelectListAsync(model.LeadId);
                model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
                return View(model);
            }

            var currentUserId = _userManager.GetUserId(User);
            var assignedTo = !string.IsNullOrEmpty(model.AssignedTo) ? model.AssignedTo : currentUserId;

            var activity = new Activity
            {
                CustomerId = model.CustomerId > 0 ? model.CustomerId : null,
                LeadId = model.LeadId > 0 ? model.LeadId : null,
                ActivityType = model.ActivityType,
                Subject = model.Subject.Trim(),
                Description = model.Description?.Trim(),
                ActivityDate = model.ActivityDate,
                Status = model.Status,
                AssignedTo = assignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Create",
                entityName: "Activity",
                recordId: activity.ActivityId.ToString(),
                oldValue: null,
                newValue: $"Type: {activity.ActivityType}, Subject: {activity.Subject}",
                details: $"Activity '{activity.Subject}' recorded",
                result: "Success"
            );

            TempData["Success"] = $"Activity '{activity.Subject}' logged successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Activities/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var activity = await _context.Activities.FindAsync(id);
            if (activity == null) return NotFound();

            if (!_scopeService.CanAccessActivity(activity, User))
            {
                return Forbid();
            }

            var vm = new ActivityCreateEditViewModel
            {
                ActivityId = activity.ActivityId,
                CustomerId = activity.CustomerId,
                LeadId = activity.LeadId,
                ActivityType = activity.ActivityType,
                Subject = activity.Subject,
                Description = activity.Description,
                ActivityDate = activity.ActivityDate,
                Status = activity.Status,
                AssignedTo = activity.AssignedTo,
                CustomersList = await GetCustomersSelectListAsync(activity.CustomerId),
                LeadsList = await GetLeadsSelectListAsync(activity.LeadId),
                UsersList = await GetUsersSelectListAsync(activity.AssignedTo)
            };

            return View(vm);
        }

        // POST: Activities/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ActivityCreateEditViewModel model)
        {
            if (id != model.ActivityId) return NotFound();

            var activity = await _context.Activities.FindAsync(id);
            if (activity == null) return NotFound();

            if (!_scopeService.CanAccessActivity(activity, User))
            {
                return Forbid();
            }

            if (!AllowedTypes.Contains(model.ActivityType))
            {
                ModelState.AddModelError("ActivityType", $"Activity Type must be one of: {string.Join(", ", AllowedTypes)}");
            }

            if (!ModelState.IsValid)
            {
                model.CustomersList = await GetCustomersSelectListAsync(model.CustomerId);
                model.LeadsList = await GetLeadsSelectListAsync(model.LeadId);
                model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
                return View(model);
            }

            var oldSummary = $"Type: {activity.ActivityType}, Subject: {activity.Subject}";

            activity.CustomerId = model.CustomerId > 0 ? model.CustomerId : null;
            activity.LeadId = model.LeadId > 0 ? model.LeadId : null;
            activity.ActivityType = model.ActivityType;
            activity.Subject = model.Subject.Trim();
            activity.Description = model.Description?.Trim();
            activity.ActivityDate = model.ActivityDate;
            activity.Status = model.Status;

            if (_scopeService.IsAdmin(User) || _scopeService.IsManager(User))
            {
                activity.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            var newSummary = $"Type: {activity.ActivityType}, Subject: {activity.Subject}";

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "Activity",
                recordId: activity.ActivityId.ToString(),
                oldValue: oldSummary,
                newValue: newSummary,
                details: $"Activity '{activity.Subject}' updated",
                result: "Success"
            );

            TempData["Success"] = $"Activity '{activity.Subject}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Activities/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null) return NotFound();

            if (!_scopeService.CanAccessActivity(activity, User))
            {
                return Forbid();
            }

            var currentUserId = _userManager.GetUserId(User);
            var subject = activity.Subject;

            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Delete",
                entityName: "Activity",
                recordId: id.ToString(),
                oldValue: subject,
                newValue: null,
                details: $"Activity '{subject}' deleted",
                result: "Success"
            );

            TempData["Success"] = $"Activity '{subject}' deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<SelectList> GetCustomersSelectListAsync(int? selectedId = null)
        {
            var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
            return new SelectList(customers, "CustomerId", "CustomerName", selectedId);
        }

        private async Task<SelectList> GetLeadsSelectListAsync(int? selectedId = null)
        {
            var leads = await _context.Leads.OrderBy(l => l.LeadName).ToListAsync();
            return new SelectList(leads, "LeadId", "LeadName", selectedId);
        }

        private async Task<SelectList> GetUsersSelectListAsync(string? selectedId = null)
        {
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            return new SelectList(users, "Id", "FullName", selectedId);
        }
    }
}
