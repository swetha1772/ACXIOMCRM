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
    [Route("api/followups")]
    [Authorize]
    public class FollowUpsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        public FollowUpsApiController(
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

        // GET: /api/followups
        [HttpGet]
        public async Task<IActionResult> GetFollowUps([FromQuery] string? status)
        {
            var query = _scopeService.ApplyFollowUpScope(
                _context.FollowUps
                    .Include(f => f.Customer)
                    .Include(f => f.Lead)
                    .Include(f => f.AssignedUser),
                User);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(f => f.Status == status);
            }

            var list = await query.OrderBy(f => f.FollowUpDate)
                .Select(f => new FollowUpResponseDto
                {
                    FollowUpId = f.FollowUpId,
                    CustomerId = f.CustomerId,
                    CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                    LeadId = f.LeadId,
                    LeadName = f.Lead != null ? f.Lead.LeadName : null,
                    Subject = f.Subject,
                    FollowUpDate = f.FollowUpDate,
                    FollowUpType = f.FollowUpType,
                    Status = f.Status,
                    Remarks = f.Remarks,
                    AssignedUserName = f.AssignedUser != null ? f.AssignedUser.FullName : null,
                    CreatedDate = f.CreatedDate
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET: /api/followups/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFollowUp(int id)
        {
            var fu = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedUser)
                .FirstOrDefaultAsync(f => f.FollowUpId == id);

            if (fu == null) return NotFound(new { message = $"Follow-up with ID {id} not found." });

            if (!_scopeService.CanAccessFollowUp(fu, User))
            {
                return StatusCode(403, new { message = "You are not authorized to view this follow-up." });
            }

            var dto = new FollowUpResponseDto
            {
                FollowUpId = fu.FollowUpId,
                CustomerId = fu.CustomerId,
                CustomerName = fu.Customer != null ? fu.Customer.CustomerName : null,
                LeadId = fu.LeadId,
                LeadName = fu.Lead != null ? fu.Lead.LeadName : null,
                Subject = fu.Subject,
                FollowUpDate = fu.FollowUpDate,
                FollowUpType = fu.FollowUpType,
                Status = fu.Status,
                Remarks = fu.Remarks,
                AssignedUserName = fu.AssignedUser != null ? fu.AssignedUser.FullName : null,
                CreatedDate = fu.CreatedDate
            };

            return Ok(dto);
        }

        // POST: /api/followups
        [HttpPost]
        public async Task<IActionResult> CreateFollowUp([FromBody] CreateFollowUpRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (dto.Status == "Planned" && dto.FollowUpDate.Date < DateTime.Today)
            {
                return BadRequest(new { message = "Follow-up date cannot be earlier than today." });
            }

            var currentUserId = _userManager.GetUserId(User);

            var followUp = new FollowUp
            {
                CustomerId = dto.CustomerId > 0 ? dto.CustomerId : null,
                LeadId = dto.LeadId > 0 ? dto.LeadId : null,
                Subject = dto.Subject.Trim(),
                FollowUpDate = dto.FollowUpDate,
                FollowUpType = dto.FollowUpType,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Planned" : dto.Status,
                Remarks = dto.Remarks?.Trim(),
                AssignedTo = currentUserId,
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
                newValue: followUp.Subject,
                details: $"Follow-up scheduled via REST API: {followUp.Subject}",
                result: "Success"
            );

            var responseDto = new FollowUpResponseDto
            {
                FollowUpId = followUp.FollowUpId,
                CustomerId = followUp.CustomerId,
                LeadId = followUp.LeadId,
                Subject = followUp.Subject,
                FollowUpDate = followUp.FollowUpDate,
                FollowUpType = followUp.FollowUpType,
                Status = followUp.Status,
                Remarks = followUp.Remarks,
                AssignedUserName = User.Identity?.Name,
                CreatedDate = followUp.CreatedDate
            };

            return CreatedAtAction(nameof(GetFollowUp), new { id = followUp.FollowUpId }, responseDto);
        }

        // PUT: /api/followups/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFollowUp(int id, [FromBody] UpdateFollowUpRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound(new { message = $"Follow-up with ID {id} not found." });

            if (!_scopeService.CanAccessFollowUp(followUp, User))
            {
                return StatusCode(403, new { message = "You are not authorized to update this follow-up." });
            }

            if (dto.Status == "Planned" && dto.FollowUpDate.Date < DateTime.Today)
            {
                return BadRequest(new { message = "Follow-up date cannot be earlier than today." });
            }

            followUp.CustomerId = dto.CustomerId > 0 ? dto.CustomerId : null;
            followUp.LeadId = dto.LeadId > 0 ? dto.LeadId : null;
            followUp.Subject = dto.Subject.Trim();
            followUp.FollowUpDate = dto.FollowUpDate;
            followUp.FollowUpType = dto.FollowUpType;
            followUp.Status = dto.Status;
            followUp.Remarks = dto.Remarks?.Trim();

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "FollowUp",
                recordId: followUp.FollowUpId.ToString(),
                oldValue: null,
                newValue: followUp.Subject,
                details: $"Follow-up updated via REST API: {followUp.Subject}",
                result: "Success"
            );

            return Ok(new { message = "Follow-up updated successfully" });
        }

        // DELETE: /api/followups/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFollowUp(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound(new { message = $"Follow-up with ID {id} not found." });

            if (!_scopeService.CanAccessFollowUp(followUp, User))
            {
                return StatusCode(403, new { message = "You are not authorized to delete this follow-up." });
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
                details: $"Follow-up deleted via REST API: {subject}",
                result: "Success"
            );

            return Ok(new { message = "Follow-up deleted successfully" });
        }
    }
}
