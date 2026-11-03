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
    public class FollowUpsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        public FollowUpsController(
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

        // GET: FollowUps
        public async Task<IActionResult> Index(string? statusFilter, string? typeFilter, DateTime? dateFilter, int page = 1)
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

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                query = query.Where(f => f.FollowUpType == typeFilter);
            }

            if (dateFilter.HasValue)
            {
                var target = dateFilter.Value.Date;
                query = query.Where(f => f.FollowUpDate.Date == target);
            }

            var totalItems = await query.CountAsync();
            var pageSize = 10;
            var followUps = await query
                .OrderBy(f => f.Status == "Planned" ? 0 : 1)
                .ThenBy(f => f.FollowUpDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new FollowUpListViewModel
            {
                StatusFilter = statusFilter,
                TypeFilter = typeFilter,
                DateFilter = dateFilter,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                FollowUps = followUps
            };

            return View(vm);
        }

        // GET: FollowUps/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var followUp = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedUser)
                .FirstOrDefaultAsync(m => m.FollowUpId == id);

            if (followUp == null) return NotFound();

            if (!_scopeService.CanAccessFollowUp(followUp, User))
            {
                return Forbid();
            }

            return View(followUp);
        }

        // GET: FollowUps/Create
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            var vm = new FollowUpCreateEditViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                FollowUpDate = DateTime.Today.AddDays(1),
                FollowUpType = "Call",
                Status = "Planned",
                CustomersList = await GetCustomersSelectListAsync(customerId),
                LeadsList = await GetLeadsSelectListAsync(leadId),
                UsersList = await GetUsersSelectListAsync()
            };

            return View(vm);
        }

        // POST: FollowUps/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUpCreateEditViewModel model)
        {
            // Business validation: Follow-up date cannot be earlier than today for a new/planned follow-up
            if (model.Status == "Planned" && model.FollowUpDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
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

            var followUp = new FollowUp
            {
                CustomerId = model.CustomerId > 0 ? model.CustomerId : null,
                LeadId = model.LeadId > 0 ? model.LeadId : null,
                Subject = model.Subject.Trim(),
                FollowUpDate = model.FollowUpDate,
                FollowUpType = model.FollowUpType,
                Status = model.Status,
                Remarks = model.Remarks?.Trim(),
                AssignedTo = assignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Create",
                entityName: "FollowUp",
                recordId: followUp.FollowUpId.ToString(),
                oldValue: null,
                newValue: $"Subject: {followUp.Subject}, Date: {followUp.FollowUpDate:yyyy-MM-dd}",
                details: $"Follow-up '{followUp.Subject}' scheduled",
                result: "Success"
            );

            TempData["Success"] = $"Follow-up '{followUp.Subject}' scheduled successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: FollowUps/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!_scopeService.CanAccessFollowUp(followUp, User))
            {
                return Forbid();
            }

            var vm = new FollowUpCreateEditViewModel
            {
                FollowUpId = followUp.FollowUpId,
                CustomerId = followUp.CustomerId,
                LeadId = followUp.LeadId,
                Subject = followUp.Subject,
                FollowUpDate = followUp.FollowUpDate,
                FollowUpType = followUp.FollowUpType,
                Status = followUp.Status,
                Remarks = followUp.Remarks,
                AssignedTo = followUp.AssignedTo,
                CustomersList = await GetCustomersSelectListAsync(followUp.CustomerId),
                LeadsList = await GetLeadsSelectListAsync(followUp.LeadId),
                UsersList = await GetUsersSelectListAsync(followUp.AssignedTo)
            };

            return View(vm);
        }

        // POST: FollowUps/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FollowUpCreateEditViewModel model)
        {
            if (id != model.FollowUpId) return NotFound();

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!_scopeService.CanAccessFollowUp(followUp, User))
            {
                return Forbid();
            }

            if (model.Status == "Planned" && model.FollowUpDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
            }

            if (!ModelState.IsValid)
            {
                model.CustomersList = await GetCustomersSelectListAsync(model.CustomerId);
                model.LeadsList = await GetLeadsSelectListAsync(model.LeadId);
                model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
                return View(model);
            }

            var oldSummary = $"Status: {followUp.Status}, Date: {followUp.FollowUpDate:yyyy-MM-dd}";

            followUp.CustomerId = model.CustomerId > 0 ? model.CustomerId : null;
            followUp.LeadId = model.LeadId > 0 ? model.LeadId : null;
            followUp.Subject = model.Subject.Trim();
            followUp.FollowUpDate = model.FollowUpDate;
            followUp.FollowUpType = model.FollowUpType;
            followUp.Status = model.Status;
            followUp.Remarks = model.Remarks?.Trim();

            if (_scopeService.IsAdmin(User) || _scopeService.IsManager(User))
            {
                followUp.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            var newSummary = $"Status: {followUp.Status}, Date: {followUp.FollowUpDate:yyyy-MM-dd}";

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "FollowUp",
                recordId: followUp.FollowUpId.ToString(),
                oldValue: oldSummary,
                newValue: newSummary,
                details: $"Follow-up '{followUp.Subject}' updated",
                result: "Success"
            );

            TempData["Success"] = $"Follow-up '{followUp.Subject}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FollowUps/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!_scopeService.CanAccessFollowUp(followUp, User))
            {
                return Forbid();
            }

            followUp.Status = "Completed";
            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "FollowUp",
                recordId: followUp.FollowUpId.ToString(),
                oldValue: "Status: Planned",
                newValue: "Status: Completed",
                details: $"Follow-up '{followUp.Subject}' marked as completed",
                result: "Success"
            );

            TempData["Success"] = $"Follow-up marked as Completed.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FollowUps/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!_scopeService.CanAccessFollowUp(followUp, User))
            {
                return Forbid();
            }

            var currentUserId = _userManager.GetUserId(User);
            var subject = followUp.Subject;

            _context.FollowUps.Remove(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Delete",
                entityName: "FollowUp",
                recordId: id.ToString(),
                oldValue: subject,
                newValue: null,
                details: $"Follow-up '{subject}' deleted",
                result: "Success"
            );

            TempData["Success"] = $"Follow-up '{subject}' deleted successfully.";
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
