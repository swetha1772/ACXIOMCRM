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
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly string[] AllowedStatuses = { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };

        public LeadsController(
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

        // GET: Leads
        public async Task<IActionResult> Index(string? searchTerm, string? statusFilter, string? assignedUserFilter, int page = 1)
        {
            var query = _scopeService.ApplyLeadScope(_context.Leads.Include(l => l.AssignedUser), User);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(l =>
                    l.LeadName.ToLower().Contains(term) ||
                    l.Email.ToLower().Contains(term) ||
                    l.Phone.ToLower().Contains(term) ||
                    (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(l => l.Status == statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(assignedUserFilter))
            {
                query = query.Where(l => l.AssignedTo == assignedUserFilter);
            }

            var totalItems = await query.CountAsync();
            var pageSize = 10;
            var leads = await query
                .OrderByDescending(l => l.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new LeadListViewModel
            {
                SearchTerm = searchTerm,
                StatusFilter = statusFilter,
                AssignedUserFilter = assignedUserFilter,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Leads = leads,
                UsersList = await GetUsersSelectListAsync(assignedUserFilter)
            };

            return View(vm);
        }

        // GET: Leads/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .Include(l => l.AssignedUser)
                .Include(l => l.ConvertedCustomer)
                .Include(l => l.Opportunities)
                .Include(l => l.FollowUps)
                .Include(l => l.Activities)
                .FirstOrDefaultAsync(m => m.LeadId == id);

            if (lead == null) return NotFound();

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return Forbid();
            }

            return View(lead);
        }

        // GET: Leads/Create
        public async Task<IActionResult> Create()
        {
            var vm = new LeadCreateEditViewModel
            {
                Status = "New",
                Priority = "Medium",
                UsersList = await GetUsersSelectListAsync()
            };
            return View(vm);
        }

        // POST: Leads/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeadCreateEditViewModel model)
        {
            if (!AllowedStatuses.Contains(model.Status))
            {
                ModelState.AddModelError("Status", $"Status must be one of: {string.Join(", ", AllowedStatuses)}");
            }

            if (!ModelState.IsValid)
            {
                model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
                return View(model);
            }

            var currentUserId = _userManager.GetUserId(User);
            var assignedTo = !string.IsNullOrEmpty(model.AssignedTo) ? model.AssignedTo : currentUserId;

            var lastId = await _context.Leads.OrderByDescending(l => l.LeadId).Select(l => l.LeadId).FirstOrDefaultAsync();
            var leadCode = $"LEAD-{1000 + lastId + 1}";

            var lead = new Lead
            {
                LeadCode = leadCode,
                LeadName = model.LeadName.Trim(),
                Email = model.Email.Trim().ToLower(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName?.Trim(),
                Source = model.Source,
                Status = model.Status,
                Priority = model.Priority,
                ExpectedValue = model.ExpectedValue,
                Notes = model.Notes?.Trim(),
                CreatedDate = DateTime.UtcNow,
                AssignedTo = assignedTo
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Create",
                entityName: "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: null,
                newValue: lead.LeadName,
                details: $"Lead '{lead.LeadName}' ({lead.LeadCode}) created",
                result: "Success"
            );

            TempData["Success"] = $"Lead '{lead.LeadName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return Forbid();
            }

            var vm = new LeadCreateEditViewModel
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Source = lead.Source,
                Status = lead.Status,
                Priority = lead.Priority,
                ExpectedValue = lead.ExpectedValue,
                AssignedTo = lead.AssignedTo,
                Notes = lead.Notes,
                UsersList = await GetUsersSelectListAsync(lead.AssignedTo)
            };

            return View(vm);
        }

        // POST: Leads/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LeadCreateEditViewModel model)
        {
            if (id != model.LeadId) return NotFound();

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return Forbid();
            }

            if (!AllowedStatuses.Contains(model.Status))
            {
                ModelState.AddModelError("Status", $"Status must be one of: {string.Join(", ", AllowedStatuses)}");
            }

            if (lead.Status == "Converted" && model.Status != "Converted")
            {
                ModelState.AddModelError("Status", "A converted lead cannot have its status reverted.");
            }

            if (!ModelState.IsValid)
            {
                model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
                return View(model);
            }

            var oldSummary = $"Name: {lead.LeadName}, Status: {lead.Status}, Value: {lead.ExpectedValue}";

            lead.LeadName = model.LeadName.Trim();
            lead.Email = model.Email.Trim().ToLower();
            lead.Phone = model.Phone.Trim();
            lead.CompanyName = model.CompanyName?.Trim();
            lead.Source = model.Source;
            lead.Status = model.Status;
            lead.Priority = model.Priority;
            lead.ExpectedValue = model.ExpectedValue;
            lead.Notes = model.Notes?.Trim();

            if (_scopeService.IsAdmin(User) || _scopeService.IsManager(User))
            {
                lead.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            var newSummary = $"Name: {lead.LeadName}, Status: {lead.Status}, Value: {lead.ExpectedValue}";

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: oldSummary,
                newValue: newSummary,
                details: $"Lead '{lead.LeadName}' updated",
                result: "Success"
            );

            TempData["Success"] = $"Lead '{lead.LeadName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Leads/Convert/5
        public async Task<IActionResult> Convert(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return Forbid();
            }

            if (lead.Status == "Converted")
            {
                TempData["Warning"] = "This lead has already been converted.";
                return RedirectToAction(nameof(Details), new { id = lead.LeadId });
            }

            var vm = new LeadConvertViewModel
            {
                LeadId = lead.LeadId,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                OpportunityName = $"{lead.LeadName} - Expansion Deal",
                Amount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 5000,
                Probability = 30,
                ExpectedCloseDate = DateTime.Today.AddDays(30),
                Stage = "Qualification"
            };

            return View(vm);
        }

        // POST: Leads/Convert/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(LeadConvertViewModel model)
        {
            var lead = await _context.Leads.FindAsync(model.LeadId);
            if (lead == null) return NotFound();

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return Forbid();
            }

            if (lead.Status == "Converted")
            {
                TempData["Warning"] = "This lead has already been converted.";
                return RedirectToAction(nameof(Details), new { id = lead.LeadId });
            }

            if (model.CreateOpportunity)
            {
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
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUserId = _userManager.GetUserId(User);

            // 1. Create or link customer
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == lead.Email.ToLower());
            if (customer == null)
            {
                var lastCustId = await _context.Customers.OrderByDescending(c => c.CustomerId).Select(c => c.CustomerId).FirstOrDefaultAsync();
                customer = new Customer
                {
                    CustomerCode = $"CUST-{1000 + lastCustId + 1}",
                    CustomerName = lead.LeadName,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    CompanyName = lead.CompanyName,
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = currentUserId,
                    OwnerId = lead.AssignedTo ?? currentUserId
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // 2. Optionally Create Opportunity
            Opportunity? opp = null;
            if (model.CreateOpportunity)
            {
                opp = new Opportunity
                {
                    OpportunityName = string.IsNullOrWhiteSpace(model.OpportunityName) ? $"{lead.LeadName} Deal" : model.OpportunityName.Trim(),
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.Amount,
                    Stage = model.Stage,
                    Probability = model.Probability,
                    ExpectedCloseDate = model.ExpectedCloseDate,
                    Status = "Open",
                    AssignedTo = lead.AssignedTo ?? currentUserId,
                    CreatedDate = DateTime.UtcNow,
                    Notes = $"Converted from Lead {lead.LeadCode}"
                };
                _context.Opportunities.Add(opp);
                await _context.SaveChangesAsync();
            }

            // 3. Mark Lead as Converted
            lead.Status = "Converted";
            lead.ConvertedCustomerId = customer.CustomerId;
            await _context.SaveChangesAsync();

            // 4. Audit Log
            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Lead Conversion",
                entityName: "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: "Status: Qualified",
                newValue: $"Converted to CustomerId: {customer.CustomerId}, OppId: {opp?.OpportunityId}",
                details: $"Lead '{lead.LeadName}' successfully converted",
                result: "Success"
            );

            TempData["Success"] = $"Lead '{lead.LeadName}' was successfully converted to customer '{customer.CustomerName}'!";
            return RedirectToAction(nameof(Details), new { id = lead.LeadId });
        }

        // POST: Leads/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return Forbid();
            }

            var currentUserId = _userManager.GetUserId(User);
            var leadName = lead.LeadName;

            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Delete",
                entityName: "Lead",
                recordId: id.ToString(),
                oldValue: leadName,
                newValue: null,
                details: $"Lead '{leadName}' deleted",
                result: "Success"
            );

            TempData["Success"] = $"Lead '{leadName}' deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<SelectList> GetUsersSelectListAsync(string? selectedId = null)
        {
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            return new SelectList(users, "Id", "FullName", selectedId);
        }
    }
}
