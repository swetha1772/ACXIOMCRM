using System.Security.Claims;
using System.Threading.Tasks;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services
{
    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardDataAsync(ClaimsPrincipal user, string filter, DateTime? startDate = null, DateTime? endDate = null);
    }
}
