using System.Linq;
using System.Security.Claims;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Services
{
    public class CrmScopeService : ICrmScopeService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public CrmScopeService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("Admin");
        public bool IsManager(ClaimsPrincipal user) => user.IsInRole("Manager");
        public bool IsSalesExecutive(ClaimsPrincipal user) => user.IsInRole("SalesExecutive");

        public string? GetUserId(ClaimsPrincipal user) => _userManager.GetUserId(user);

        public IQueryable<Customer> ApplyCustomerScope(IQueryable<Customer> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetUserId(user);
            return query.Where(c => c.OwnerId == userId || c.CreatedBy == userId);
        }

        public IQueryable<Lead> ApplyLeadScope(IQueryable<Lead> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetUserId(user);
            return query.Where(l => l.AssignedTo == userId);
        }

        public IQueryable<Opportunity> ApplyOpportunityScope(IQueryable<Opportunity> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetUserId(user);
            return query.Where(o => o.AssignedTo == userId);
        }

        public IQueryable<FollowUp> ApplyFollowUpScope(IQueryable<FollowUp> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetUserId(user);
            return query.Where(f => f.AssignedTo == userId);
        }

        public IQueryable<Activity> ApplyActivityScope(IQueryable<Activity> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetUserId(user);
            return query.Where(a => a.AssignedTo == userId);
        }

        public bool CanAccessCustomer(Customer customer, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetUserId(user);
            return customer.OwnerId == userId || customer.CreatedBy == userId;
        }

        public bool CanAccessLead(Lead lead, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetUserId(user);
            return lead.AssignedTo == userId;
        }

        public bool CanAccessOpportunity(Opportunity opportunity, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetUserId(user);
            return opportunity.AssignedTo == userId;
        }

        public bool CanAccessFollowUp(FollowUp followUp, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetUserId(user);
            return followUp.AssignedTo == userId;
        }

        public bool CanAccessActivity(Activity activity, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetUserId(user);
            return activity.AssignedTo == userId;
        }
    }
}
