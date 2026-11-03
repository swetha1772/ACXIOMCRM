using System;
using System.Linq;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmScopeService _scopeService;

        public ReportsApiController(ApplicationDbContext context, ICrmScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        // GET: /api/reports/pipeline
        [HttpGet("pipeline")]
        public async Task<IActionResult> GetPipelineReport()
        {
            var query = _scopeService.ApplyOpportunityScope(_context.Opportunities.Include(o => o.AssignedUser), User);
            var list = await query.ToListAsync();

            var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var byStage = stages.Select(s => new
            {
                stage = s,
                count = list.Count(o => o.Stage == s),
                totalAmount = list.Where(o => o.Stage == s).Sum(o => o.Amount),
                weightedAmount = Math.Round(list.Where(o => o.Stage == s).Sum(o => o.WeightedAmount), 2)
            });

            var totalPipeline = list.Where(o => o.Stage != "Lost").Sum(o => o.Amount);
            var totalWeighted = Math.Round(list.Where(o => o.Stage != "Lost").Sum(o => o.WeightedAmount), 2);

            return Ok(new
            {
                totalPipeline,
                totalWeighted,
                stages = byStage
            });
        }
    }
}
