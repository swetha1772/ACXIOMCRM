using System;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AcxiomCRM.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(
            string userId,
            string? userName,
            string action,
            string entityName,
            string? recordId,
            string? oldValue,
            string? newValue,
            string? details = null,
            string? ipAddress = null,
            string result = "Success")
        {
            try
            {
                var ip = ipAddress ?? _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";

                var entry = new AuditLog
                {
                    UserId = string.IsNullOrWhiteSpace(userId) ? "Anonymous" : userId,
                    UserName = userName,
                    Action = action,
                    EntityName = entityName,
                    RecordId = recordId,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Details = details,
                    IpAddress = ip,
                    Result = result,
                    CreatedDate = DateTime.UtcNow
                };

                _context.AuditLogs.Add(entry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Never let audit failure crash the business transaction, but log the error
                _logger.LogError(ex, "Failed to persist audit log entry for Action: {Action}, Entity: {EntityName}", action, entityName);
            }
        }
    }
}
