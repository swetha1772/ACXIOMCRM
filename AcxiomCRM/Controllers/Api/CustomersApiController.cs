using System;
using System.Linq;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class CustomersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomersApiController(
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

        // GET: /api/customers
        [HttpGet]
        public async Task<IActionResult> GetCustomers([FromQuery] string? search, [FromQuery] string? status)
        {
            var query = _scopeService.ApplyCustomerScope(_context.Customers.Include(c => c.Owner), User);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    c.Phone.ToLower().Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.Status == status);
            }

            var list = await query.OrderByDescending(c => c.CreatedDate)
                .Select(c => new CustomerResponseDto
                {
                    CustomerId = c.CustomerId,
                    CustomerCode = c.CustomerCode,
                    CustomerName = c.CustomerName,
                    Email = c.Email,
                    Phone = c.Phone,
                    CompanyName = c.CompanyName,
                    Address = c.Address,
                    City = c.City,
                    State = c.State,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    OwnerName = c.Owner != null ? c.Owner.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET: /api/customers/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            if (!_scopeService.CanAccessCustomer(customer, User))
            {
                return StatusCode(403, new { message = "You are not authorized to access this customer record." });
            }

            var dto = new CustomerResponseDto
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
                CreatedDate = customer.CreatedDate,
                OwnerName = customer.Owner != null ? customer.Owner.FullName : null
            };

            return Ok(dto);
        }

        // POST: /api/customers
        [HttpPost]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new { message = "A customer with this email address already exists." });
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
            {
                return Conflict(new { message = "A customer with this phone number already exists." });
            }

            var currentUserId = _userManager.GetUserId(User);
            var lastId = await _context.Customers.OrderByDescending(c => c.CustomerId).Select(c => c.CustomerId).FirstOrDefaultAsync();
            var customerCode = $"CUST-{1000 + lastId + 1}";

            var customer = new Customer
            {
                CustomerCode = customerCode,
                CustomerName = dto.CustomerName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                Phone = dto.Phone.Trim(),
                CompanyName = dto.CompanyName?.Trim(),
                Address = dto.Address?.Trim(),
                City = dto.City?.Trim(),
                State = dto.State?.Trim(),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = currentUserId,
                OwnerId = currentUserId
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
                details: $"Customer created via REST API: {customer.CustomerName}",
                result: "Success"
            );

            var responseDto = new CustomerResponseDto
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
                CreatedDate = customer.CreatedDate,
                OwnerName = User.Identity?.Name
            };

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, responseDto);
        }

        // PUT: /api/customers/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            if (!_scopeService.CanAccessCustomer(customer, User))
            {
                return StatusCode(403, new { message = "You are not authorized to update this customer record." });
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new { message = "A customer with this email address already exists." });
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == dto.Phone))
            {
                return Conflict(new { message = "A customer with this phone number already exists." });
            }

            customer.CustomerName = dto.CustomerName.Trim();
            customer.Email = dto.Email.Trim().ToLower();
            customer.Phone = dto.Phone.Trim();
            customer.CompanyName = dto.CompanyName?.Trim();
            customer.Address = dto.Address?.Trim();
            customer.City = dto.City?.Trim();
            customer.State = dto.State?.Trim();
            customer.Status = dto.Status;
            customer.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "Customer",
                recordId: customer.CustomerId.ToString(),
                oldValue: null,
                newValue: customer.CustomerName,
                details: $"Customer updated via REST API: {customer.CustomerName}",
                result: "Success"
            );

            return Ok(new { message = "Customer updated successfully" });
        }

        // DELETE: /api/customers/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            if (!_scopeService.CanAccessCustomer(customer, User))
            {
                return StatusCode(403, new { message = "You are not authorized to delete this customer record." });
            }

            var currentUserId = _userManager.GetUserId(User);
            var name = customer.CustomerName;

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Delete",
                entityName: "Customer",
                recordId: id.ToString(),
                oldValue: name,
                newValue: null,
                details: $"Customer deleted via REST API: {name}",
                result: "Success"
            );

            return Ok(new { message = "Customer deleted successfully" });
        }
    }
}
