using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface IAuditService
    {
        Task LogAsync(string userId, string? userName, string action, string entityName, string? recordId, string? oldValue, string? newValue, string? details = null, string? ipAddress = null, string result = "Success");
    }
}
