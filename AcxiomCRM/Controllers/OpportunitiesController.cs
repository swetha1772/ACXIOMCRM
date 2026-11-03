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
    public class OpportunitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly string[] AllowedStages = { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };

        public OpportunitiesController(
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

        // GET: Opportunities
        public async Task<IActionResult> Index(string? searchTerm, string? stageFilter, string? statusFilter, int page = 1)
        {
            var query = _scopeService.ApplyOpportunityScope(
                _context.Opportunities
                    .Include(o => o.Customer)
                    .Include(o => o.AssignedUser),
                User);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(o =>
                    o.OpportunityName.ToLower().Contains(term) ||
                    (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(stageFilter))
            {
                query = query.Where(o => o.Stage == stageFilter);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(o => o.Status == statusFilter);
            }

            var totalItems = await query.CountAsync();
            var pageSize = 10;
            var opps = await query
                .OrderByDescending(o => o.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new OpportunityListViewModel
            {
                SearchTerm = searchTerm,
                StageFilter = stageFilter,
                StatusFilter = statusFilter,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Opportunities = opps
            };

            return View(vm);
        }

        // GET: Opportunities/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var opportunity = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .FirstOrDefaultAsync(m => m.OpportunityId == id);

            if (opportunity == null) return NotFound();

            if (!_scopeService.CanAccessOpportunity(opportunity, User))
            {
                return Forbid();
            }

            return View(opportunity);
        }

        // GET: Opportunities/Create
        public async Task<IActionResult> Create(int? customerId = null)
        {
            var vm = new OpportunityCreateEditViewModel
            {
                CustomerId = customerId ?? 0,
                Probability = 20,
                Stage = "Qualification",
                Status = "Open",
                ExpectedCloseDate = DateTime.Today.AddDays(30),
                CustomersList = await GetCustomersSelectListAsync(customerId),
                LeadsList = await GetLeadsSelectListAsync(),
                UsersList = await GetUsersSelectListAsync()
            };

            return View(vm);
        }

        // POST: Opportunities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OpportunityCreateEditViewModel model)
        {
            // Server-side business validations
            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            if (model.Probability < 0 || model.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            if (model.ExpectedCloseDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (!AllowedStages.Contains(model.Stage))
            {
                ModelState.AddModelError("Stage", $"Stage must be one of: {string.Join(", ", AllowedStages)}");
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

            var status = model.Stage switch
            {
                "Won" => "Won",
                "Lost" => "Lost",
                _ => "Open"
            };

            var opportunity = new Opportunity
            {
                OpportunityName = model.OpportunityName.Trim(),
                CustomerId = model.CustomerId,
                LeadId = model.LeadId,
                Amount = model.Amount,
                Stage = model.Stage,
                Probability = model.Probability,
                ExpectedCloseDate = model.ExpectedCloseDate,
                Status = status,
                Notes = model.Notes?.Trim(),
                CreatedDate = DateTime.UtcNow,
                AssignedTo = assignedTo
            };

            _context.Opportunities.Add(opportunity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Create",
                entityName: "Opportunity",
                recordId: opportunity.OpportunityId.ToString(),
                oldValue: null,
                newValue: $"Name: {opportunity.OpportunityName}, Amount: {opportunity.Amount}, Stage: {opportunity.Stage}",
                details: $"Opportunity '{opportunity.OpportunityName}' created",
                result: "Success"
            );

            TempData["Success"] = $"Opportunity '{opportunity.OpportunityName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Opportunities/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var opportunity = await _context.Opportunities.FindAsync(id);
            if (opportunity == null) return NotFound();

            if (!_scopeService.CanAccessOpportunity(opportunity, User))
            {
                return Forbid();
            }

            var vm = new OpportunityCreateEditViewModel
            {
                OpportunityId = opportunity.OpportunityId,
                OpportunityName = opportunity.OpportunityName,
                CustomerId = opportunity.CustomerId,
                LeadId = opportunity.LeadId,
                Amount = opportunity.Amount,
                Stage = opportunity.Stage,
                Probability = opportunity.Probability,
                ExpectedCloseDate = opportunity.ExpectedCloseDate,
                Status = opportunity.Status,
                AssignedTo = opportunity.AssignedTo,
                Notes = opportunity.Notes,
                CustomersList = await GetCustomersSelectListAsync(opportunity.CustomerId),
                LeadsList = await GetLeadsSelectListAsync(opportunity.LeadId),
                UsersList = await GetUsersSelectListAsync(opportunity.AssignedTo)
            };

            return View(vm);
        }

        // POST: Opportunities/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OpportunityCreateEditViewModel model)
        {
            if (id != model.OpportunityId) return NotFound();

            var opportunity = await _context.Opportunities.FindAsync(id);
            if (opportunity == null) return NotFound();

            if (!_scopeService.CanAccessOpportunity(opportunity, User))
            {
                return Forbid();
            }

            // Server-side business validations
            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            if (model.Probability < 0 || model.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            // For active opportunities, close date cannot be in the past
            if (model.Stage != "Won" && model.Stage != "Lost" && model.ExpectedCloseDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                model.CustomersList = await GetCustomersSelectListAsync(model.CustomerId);
                model.LeadsList = await GetLeadsSelectListAsync(model.LeadId);
                model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
                return View(model);
            }

            var oldSummary = $"Stage: {opportunity.Stage}, Amount: {opportunity.Amount}, Prob: {opportunity.Probability}";

            opportunity.OpportunityName = model.OpportunityName.Trim();
            opportunity.CustomerId = model.CustomerId;
            opportunity.LeadId = model.LeadId;
            opportunity.Amount = model.Amount;
            opportunity.Stage = model.Stage;
            opportunity.Probability = model.Probability;
            opportunity.ExpectedCloseDate = model.ExpectedCloseDate;
            opportunity.Status = model.Stage switch
            {
                "Won" => "Won",
                "Lost" => "Lost",
                _ => "Open"
            };
            opportunity.Notes = model.Notes?.Trim();

            if (_scopeService.IsAdmin(User) || _scopeService.IsManager(User))
            {
                opportunity.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            var newSummary = $"Stage: {opportunity.Stage}, Amount: {opportunity.Amount}, Prob: {opportunity.Probability}";

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "Opportunity",
                recordId: opportunity.OpportunityId.ToString(),
                oldValue: oldSummary,
                newValue: newSummary,
                details: $"Opportunity '{opportunity.OpportunityName}' updated",
                result: "Success"
            );

            TempData["Success"] = $"Opportunity '{opportunity.OpportunityName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Opportunities/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var opportunity = await _context.Opportunities.FindAsync(id);
            if (opportunity == null) return NotFound();

            if (!_scopeService.CanAccessOpportunity(opportunity, User))
            {
                return Forbid();
            }

            var currentUserId = _userManager.GetUserId(User);
            var oppName = opportunity.OpportunityName;

            _context.Opportunities.Remove(opportunity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Delete",
                entityName: "Opportunity",
                recordId: id.ToString(),
                oldValue: oppName,
                newValue: null,
                details: $"Opportunity '{oppName}' deleted",
                result: "Success"
            );

            TempData["Success"] = $"Opportunity '{oppName}' deleted successfully.";
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
