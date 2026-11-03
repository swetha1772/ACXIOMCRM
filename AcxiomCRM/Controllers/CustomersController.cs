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
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomersController(
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

        // GET: Customers
        public async Task<IActionResult> Index(string? searchTerm, string? statusFilter, int page = 1)
        {
            var query = _scopeService.ApplyCustomerScope(_context.Customers.Include(c => c.Owner), User);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    c.Phone.ToLower().Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(c => c.Status == statusFilter);
            }

            var totalItems = await query.CountAsync();
            var pageSize = 10;
            var customers = await query
                .OrderByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new CustomerListViewModel
            {
                SearchTerm = searchTerm,
                StatusFilter = statusFilter,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Customers = customers
            };

            return View(vm);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.Owner)
                .Include(c => c.Opportunities)
                .Include(c => c.FollowUps)
                .Include(c => c.Activities)
                .FirstOrDefaultAsync(m => m.CustomerId == id);

            if (customer == null) return NotFound();

            if (!_scopeService.CanAccessCustomer(customer, User))
            {
                return Forbid();
            }

            return View(customer);
        }

        // GET: Customers/Create
        public async Task<IActionResult> Create()
        {
            var vm = new CustomerCreateEditViewModel
            {
                Status = "Active",
                UsersList = await GetUsersSelectListAsync()
            };
            return View(vm);
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerCreateEditViewModel model)
        {
            // Business validation: Email uniqueness
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "A customer with this email address already exists.");
            }

            // Business validation: Phone uniqueness
            if (await _context.Customers.AnyAsync(c => c.Phone == model.Phone))
            {
                ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
            }

            if (!ModelState.IsValid)
            {
                model.UsersList = await GetUsersSelectListAsync();
                return View(model);
            }

            var currentUserId = _userManager.GetUserId(User);
            var assignedOwnerId = !string.IsNullOrEmpty(model.OwnerId) ? model.OwnerId : currentUserId;

            var lastId = await _context.Customers.OrderByDescending(c => c.CustomerId).Select(c => c.CustomerId).FirstOrDefaultAsync();
            var customerCode = $"CUST-{1000 + lastId + 1}";

            var customer = new Customer
            {
                CustomerCode = customerCode,
                CustomerName = model.CustomerName.Trim(),
                Email = model.Email.Trim().ToLower(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName?.Trim(),
                Address = model.Address?.Trim(),
                City = model.City?.Trim(),
                State = model.State?.Trim(),
                Status = model.Status,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = currentUserId,
                OwnerId = assignedOwnerId
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Create",
                entityName: "Customer",
                recordId: customer.CustomerId.ToString(),
                oldValue: null,
                newValue: customer.CustomerName,
                details: $"Customer '{customer.CustomerName}' ({customer.CustomerCode}) created",
                result: "Success"
            );

            TempData["Success"] = $"Customer '{customer.CustomerName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            if (!_scopeService.CanAccessCustomer(customer, User))
            {
                return Forbid();
            }

            var vm = new CustomerCreateEditViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                OwnerId = customer.OwnerId,
                UsersList = await GetUsersSelectListAsync(customer.OwnerId)
            };

            return View(vm);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerCreateEditViewModel model)
        {
            if (id != model.CustomerId) return NotFound();

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            if (!_scopeService.CanAccessCustomer(customer, User))
            {
                return Forbid();
            }

            // Uniqueness checks excluding current customer
            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == model.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "A customer with this email address already exists.");
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == model.Phone))
            {
                ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
            }

            if (!ModelState.IsValid)
            {
                model.UsersList = await GetUsersSelectListAsync(model.OwnerId);
                return View(model);
            }

            var oldSummary = $"Name: {customer.CustomerName}, Email: {customer.Email}, Status: {customer.Status}";

            customer.CustomerName = model.CustomerName.Trim();
            customer.Email = model.Email.Trim().ToLower();
            customer.Phone = model.Phone.Trim();
            customer.CompanyName = model.CompanyName?.Trim();
            customer.Address = model.Address?.Trim();
            customer.City = model.City?.Trim();
            customer.State = model.State?.Trim();
            customer.Status = model.Status;
            customer.ModifiedDate = DateTime.UtcNow;

            if (_scopeService.IsAdmin(User) || _scopeService.IsManager(User))
            {
                customer.OwnerId = model.OwnerId;
            }

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            var newSummary = $"Name: {customer.CustomerName}, Email: {customer.Email}, Status: {customer.Status}";

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "Customer",
                recordId: customer.CustomerId.ToString(),
                oldValue: oldSummary,
                newValue: newSummary,
                details: $"Customer '{customer.CustomerName}' updated",
                result: "Success"
            );

            TempData["Success"] = $"Customer '{customer.CustomerName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Customers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            if (!_scopeService.CanAccessCustomer(customer, User))
            {
                return Forbid();
            }

            var currentUserId = _userManager.GetUserId(User);
            var customerName = customer.CustomerName;

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Delete",
                entityName: "Customer",
                recordId: id.ToString(),
                oldValue: customerName,
                newValue: null,
                details: $"Customer '{customerName}' deleted",
                result: "Success"
            );

            TempData["Success"] = $"Customer '{customerName}' deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<SelectList> GetUsersSelectListAsync(string? selectedId = null)
        {
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            return new SelectList(users, "Id", "FullName", selectedId);
        }
    }
}
