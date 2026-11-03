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
    [Route("api/opportunities")]
    [Authorize]
    public class OpportunitiesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;
        private readonly IAuditService _auditService;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly string[] AllowedStages = { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };

        public OpportunitiesApiController(
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

        // GET: /api/opportunities
        [HttpGet]
        public async Task<IActionResult> GetOpportunities([FromQuery] string? stage, [FromQuery] string? status)
        {
            var query = _scopeService.ApplyOpportunityScope(
                _context.Opportunities
                    .Include(o => o.Customer)
                    .Include(o => o.AssignedUser),
                User);

            if (!string.IsNullOrWhiteSpace(stage))
            {
                query = query.Where(o => o.Stage == stage);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.Status == status);
            }

            var list = await query.OrderByDescending(o => o.CreatedDate)
                .Select(o => new OpportunityResponseDto
                {
                    OpportunityId = o.OpportunityId,
                    OpportunityName = o.OpportunityName,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.CustomerName : string.Empty,
                    LeadId = o.LeadId,
                    Amount = o.Amount,
                    Stage = o.Stage,
                    Probability = o.Probability,
                    WeightedAmount = Math.Round(o.Amount * o.Probability / 100m, 2),
                    ExpectedCloseDate = o.ExpectedCloseDate,
                    Status = o.Status,
                    CreatedDate = o.CreatedDate,
                    AssignedUserName = o.AssignedUser != null ? o.AssignedUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET: /api/opportunities/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOpportunity(int id)
        {
            var opp = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedUser)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (opp == null) return NotFound(new { message = $"Opportunity with ID {id} not found." });

            if (!_scopeService.CanAccessOpportunity(opp, User))
            {
                return StatusCode(403, new { message = "You are not authorized to view this opportunity." });
            }

            var dto = new OpportunityResponseDto
            {
                OpportunityId = opp.OpportunityId,
                OpportunityName = opp.OpportunityName,
                CustomerId = opp.CustomerId,
                CustomerName = opp.Customer != null ? opp.Customer.CustomerName : string.Empty,
                LeadId = opp.LeadId,
                Amount = opp.Amount,
                Stage = opp.Stage,
                Probability = opp.Probability,
                WeightedAmount = Math.Round(opp.Amount * opp.Probability / 100m, 2),
                ExpectedCloseDate = opp.ExpectedCloseDate,
                Status = opp.Status,
                CreatedDate = opp.CreatedDate,
                AssignedUserName = opp.AssignedUser != null ? opp.AssignedUser.FullName : null
            };

            return Ok(dto);
        }

        // POST: /api/opportunities
        [HttpPost]
        public async Task<IActionResult> CreateOpportunity([FromBody] CreateOpportunityRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
            }

            if (dto.Probability < 0 || dto.Probability > 100)
            {
                return BadRequest(new { message = "Probability must be between 0 and 100." });
            }

            if (dto.ExpectedCloseDate.Date < DateTime.Today)
            {
                return BadRequest(new { message = "Expected Close Date cannot be in the past." });
            }

            if (!AllowedStages.Contains(dto.Stage))
            {
                return BadRequest(new { message = $"Stage must be one of: {string.Join(", ", AllowedStages)}" });
            }

            var customer = await _context.Customers.FindAsync(dto.CustomerId);
            if (customer == null)
            {
                return BadRequest(new { message = $"Customer with ID {dto.CustomerId} does not exist." });
            }

            var currentUserId = _userManager.GetUserId(User);

            var status = dto.Stage switch
            {
                "Won" => "Won",
                "Lost" => "Lost",
                _ => "Open"
            };

            var opp = new Opportunity
            {
                OpportunityName = dto.OpportunityName.Trim(),
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Status = status,
                Notes = dto.Notes?.Trim(),
                CreatedDate = DateTime.UtcNow,
                AssignedTo = currentUserId
            };

            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Create",
                entityName: "Opportunity",
                recordId: opp.OpportunityId.ToString(),
                oldValue: null,
                newValue: opp.OpportunityName,
                details: $"Opportunity created via REST API: {opp.OpportunityName}",
                result: "Success"
            );

            var responseDto = new OpportunityResponseDto
            {
                OpportunityId = opp.OpportunityId,
                OpportunityName = opp.OpportunityName,
                CustomerId = opp.CustomerId,
                CustomerName = customer.CustomerName,
                LeadId = opp.LeadId,
                Amount = opp.Amount,
                Stage = opp.Stage,
                Probability = opp.Probability,
                WeightedAmount = Math.Round(opp.Amount * opp.Probability / 100m, 2),
                ExpectedCloseDate = opp.ExpectedCloseDate,
                Status = opp.Status,
                CreatedDate = opp.CreatedDate,
                AssignedUserName = User.Identity?.Name
            };

            return CreatedAtAction(nameof(GetOpportunity), new { id = opp.OpportunityId }, responseDto);
        }

        // PUT: /api/opportunities/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] UpdateOpportunityRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound(new { message = $"Opportunity with ID {id} not found." });

            if (!_scopeService.CanAccessOpportunity(opp, User))
            {
                return StatusCode(403, new { message = "You are not authorized to update this opportunity." });
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
            }

            if (dto.Probability < 0 || dto.Probability > 100)
            {
                return BadRequest(new { message = "Probability must be between 0 and 100." });
            }

            if (!AllowedStages.Contains(dto.Stage))
            {
                return BadRequest(new { message = $"Stage must be one of: {string.Join(", ", AllowedStages)}" });
            }

            opp.OpportunityName = dto.OpportunityName.Trim();
            opp.CustomerId = dto.CustomerId;
            opp.LeadId = dto.LeadId;
            opp.Amount = dto.Amount;
            opp.Stage = dto.Stage;
            opp.Probability = dto.Probability;
            opp.ExpectedCloseDate = dto.ExpectedCloseDate;
            opp.Status = dto.Stage switch
            {
                "Won" => "Won",
                "Lost" => "Lost",
                _ => "Open"
            };
            opp.Notes = dto.Notes?.Trim();

            await _context.SaveChangesAsync();

            var currentUserId = _userManager.GetUserId(User);
            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Update",
                entityName: "Opportunity",
                recordId: opp.OpportunityId.ToString(),
                oldValue: null,
                newValue: opp.OpportunityName,
                details: $"Opportunity updated via REST API: {opp.OpportunityName}",
                result: "Success"
            );

            return Ok(new { message = "Opportunity updated successfully" });
        }

        // DELETE: /api/opportunities/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOpportunity(int id)
        {
            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound(new { message = $"Opportunity with ID {id} not found." });

            if (!_scopeService.CanAccessOpportunity(opp, User))
            {
                return StatusCode(403, new { message = "You are not authorized to delete this opportunity." });
            }

            var currentUserId = _userManager.GetUserId(User);
            var name = opp.OpportunityName;

            _context.Opportunities.Remove(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId: currentUserId ?? "Unknown",
                userName: User.Identity?.Name,
                action: "Delete",
                entityName: "Opportunity",
                recordId: id.ToString(),
                oldValue: name,
                newValue: null,
                details: $"Opportunity deleted via REST API: {name}",
                result: "Success"
            );

            return Ok(new { message = "Opportunity deleted successfully" });
        }
    }
}
