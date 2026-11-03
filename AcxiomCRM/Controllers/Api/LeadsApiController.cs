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
    [Route("api/leads")]
    [Authorize]
    public class LeadsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly string[] AllowedStatuses = { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };

        public LeadsApiController(
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

        // GET: /api/leads
        [HttpGet]
        public async Task<IActionResult> GetLeads([FromQuery] string? search, [FromQuery] string? status)
        {
            var query = _scopeService.ApplyLeadScope(_context.Leads.Include(l => l.AssignedUser), User);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l =>
                    l.LeadName.ToLower().Contains(term) ||
                    l.Email.ToLower().Contains(term) ||
                    l.Phone.ToLower().Contains(term) ||
                    (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            var list = await query.OrderByDescending(l => l.CreatedDate)
                .Select(l => new LeadResponseDto
                {
                    LeadId = l.LeadId,
                    LeadCode = l.LeadCode,
                    LeadName = l.LeadName,
                    Email = l.Email,
                    Phone = l.Phone,
                    CompanyName = l.CompanyName,
                    Source = l.Source,
                    Status = l.Status,
                    Priority = l.Priority,
                    ExpectedValue = l.ExpectedValue,
                    CreatedDate = l.CreatedDate,
                    AssignedUserName = l.AssignedUser != null ? l.AssignedUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET: /api/leads/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLead(int id)
        {
            var lead = await _context.Leads
                .Include(l => l.AssignedUser)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return NotFound(new { message = $"Lead with ID {id} not found." });

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return StatusCode(403, new { message = "You are not authorized to view this lead record." });
            }

            var dto = new LeadResponseDto
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
                CreatedDate = lead.CreatedDate,
                AssignedUserName = lead.AssignedUser != null ? lead.AssignedUser.FullName : null
            };

            return Ok(dto);
        }

        // POST: /api/leads
        [HttpPost]
        public async Task<IActionResult> CreateLead([FromBody] CreateLeadRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!AllowedStatuses.Contains(dto.Status))
            {
                return BadRequest(new { message = $"Status must be one of: {string.Join(", ", AllowedStatuses)}" });
            }

            var currentUserId = _userManager.GetUserId(User);
            var lastId = await _context.Leads.OrderByDescending(l => l.LeadId).Select(l => l.LeadId).FirstOrDefaultAsync();
            var leadCode = $"LEAD-{1000 + lastId + 1}";

            var lead = new Lead
            {
                LeadCode = leadCode,
                LeadName = dto.LeadName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                Phone = dto.Phone.Trim(),
                CompanyName = dto.CompanyName?.Trim(),
                Source = dto.Source,
                Status = dto.Status,
                Priority = dto.Priority,
                ExpectedValue = dto.ExpectedValue,
                Notes = dto.Notes?.Trim(),
                CreatedDate = DateTime.UtcNow,
                AssignedTo = currentUserId
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
                details: $"Lead created via REST API: {lead.LeadName}",
                result: "Success"
            );

            var responseDto = new LeadResponseDto
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
                CreatedDate = lead.CreatedDate,
                AssignedUserName = User.Identity?.Name
            };

            return CreatedAtAction(nameof(GetLead), new { id = lead.LeadId }, responseDto);
        }

        // PUT: /api/leads/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLead(int id, [FromBody] UpdateLeadRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound(new { message = $"Lead with ID {id} not found." });

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return StatusCode(403, new { message = "You are not authorized to update this lead." });
            }

            if (!AllowedStatuses.Contains(dto.Status))
            {
                return BadRequest(new { message = $"Status must be one of: {string.Join(", ", AllowedStatuses)}" });
            }

            lead.LeadName = dto.LeadName.Trim();
            lead.Email = dto.Email.Trim().ToLower();
            lead.Phone = dto.Phone.Trim();
            lead.CompanyName = dto.CompanyName?.Trim();
            lead.Source = dto.Source;
            lead.Status = dto.Status;
            lead.Priority = dto.Priority;
            lead.ExpectedValue = dto.ExpectedValue;
            lead.Notes = dto.Notes?.Trim();

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: null,
                newValue: lead.LeadName,
                details: $"Lead updated via REST API: {lead.LeadName}",
                result: "Success"
            );

            return Ok(new { message = "Lead updated successfully" });
        }

        // DELETE: /api/leads/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLead(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound(new { message = $"Lead with ID {id} not found." });

            if (!_scopeService.CanAccessLead(lead, User))
            {
                return StatusCode(403, new { message = "You are not authorized to delete this lead." });
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
                details: $"Lead deleted via REST API: {leadName}",
                result: "Success"
            );

            return Ok(new { message = "Lead deleted successfully" });
        }
    }
}
