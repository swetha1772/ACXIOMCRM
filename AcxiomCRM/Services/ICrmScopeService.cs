using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface ICrmScopeService
    {
        bool IsAdmin(ClaimsPrincipal user);
        bool IsManager(ClaimsPrincipal user);
        bool IsSalesExecutive(ClaimsPrincipal user);
        string? GetUserId(ClaimsPrincipal user);

        IQueryable<Customer> ApplyCustomerScope(IQueryable<Customer> query, ClaimsPrincipal user);
        IQueryable<Lead> ApplyLeadScope(IQueryable<Lead> query, ClaimsPrincipal user);
        IQueryable<Opportunity> ApplyOpportunityScope(IQueryable<Opportunity> query, ClaimsPrincipal user);
        IQueryable<FollowUp> ApplyFollowUpScope(IQueryable<FollowUp> query, ClaimsPrincipal user);
        IQueryable<Activity> ApplyActivityScope(IQueryable<Activity> query, ClaimsPrincipal user);

        bool CanAccessCustomer(Customer customer, ClaimsPrincipal user);
        bool CanAccessLead(Lead lead, ClaimsPrincipal user);
        bool CanAccessOpportunity(Opportunity opportunity, ClaimsPrincipal user);
        bool CanAccessFollowUp(FollowUp followUp, ClaimsPrincipal user);
        bool CanAccessActivity(Activity activity, ClaimsPrincipal user);
    }
}
