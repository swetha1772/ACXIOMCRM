using System;
using System.Linq;
using System.Threading.Tasks;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AcxiomCRM.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.EnsureCreatedAsync();

            // 1. Seed Roles
            string[] roleNames = { "Admin", "Manager", "SalesExecutive" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Default Users
            var adminEmail = "admin@acxiom.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            var managerEmail = "manager@acxiom.com";
            var managerUser = await userManager.FindByEmailAsync(managerEmail);
            if (managerUser == null)
            {
                managerUser = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    FullName = "Sarah Jenkins",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(managerUser, "Manager@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(managerUser, "Manager");
                }
            }

            var salesEmail = "sales@acxiom.com";
            var salesUser = await userManager.FindByEmailAsync(salesEmail);
            if (salesUser == null)
            {
                salesUser = new ApplicationUser
                {
                    UserName = salesEmail,
                    Email = salesEmail,
                    FullName = "Alex Rivera",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(salesUser, "Sales@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(salesUser, "SalesExecutive");
                }
            }

            // 3. Seed Sample CRM Data if empty
            if (!context.Customers.Any())
            {
                var cust1 = new Customer
                {
                    CustomerCode = "CUST-1001",
                    CustomerName = "Apex Global Tech",
                    Email = "contact@apextech.com",
                    Phone = "+1 415-555-0199",
                    CompanyName = "Apex Global",
                    Address = "100 Market St, Suite 400",
                    City = "San Francisco",
                    State = "CA",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow.AddDays(-60),
                    CreatedBy = adminUser.Id,
                    OwnerId = salesUser.Id
                };

                var cust2 = new Customer
                {
                    CustomerCode = "CUST-1002",
                    CustomerName = "BlueStar Financials",
                    Email = "finance@bluestar.com",
                    Phone = "+1 212-555-0145",
                    CompanyName = "BlueStar Corp",
                    Address = "742 5th Ave",
                    City = "New York",
                    State = "NY",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow.AddDays(-45),
                    CreatedBy = adminUser.Id,
                    OwnerId = salesUser.Id
                };

                var cust3 = new Customer
                {
                    CustomerCode = "CUST-1003",
                    CustomerName = "Zenith Healthcare Solutions",
                    Email = "admin@zenithhealth.org",
                    Phone = "+1 312-555-0182",
                    CompanyName = "Zenith Health",
                    Address = "300 Michigan Ave",
                    City = "Chicago",
                    State = "IL",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow.AddDays(-20),
                    CreatedBy = managerUser.Id,
                    OwnerId = managerUser.Id
                };

                context.Customers.AddRange(cust1, cust2, cust3);
                await context.SaveChangesAsync();

                // Seed Leads
                var lead1 = new Lead
                {
                    LeadCode = "LEAD-1001",
                    LeadName = "David Kim",
                    Email = "david.kim@innovatecorp.com",
                    Phone = "+1 408-555-0177",
                    CompanyName = "Innovate Corp",
                    Source = "Website",
                    Status = "New",
                    Priority = "High",
                    ExpectedValue = 25000,
                    CreatedDate = DateTime.UtcNow.AddDays(-10),
                    AssignedTo = salesUser.Id,
                    Notes = "Interested in enterprise CRM migration."
                };

                var lead2 = new Lead
                {
                    LeadCode = "LEAD-1002",
                    LeadName = "Elena Rostova",
                    Email = "elena.r@quantumai.io",
                    Phone = "+1 650-555-0123",
                    CompanyName = "Quantum AI",
                    Source = "Referral",
                    Status = "Contacted",
                    Priority = "Medium",
                    ExpectedValue = 45000,
                    CreatedDate = DateTime.UtcNow.AddDays(-15),
                    AssignedTo = salesUser.Id,
                    Notes = "Requested demo session for executive team."
                };

                var lead3 = new Lead
                {
                    LeadCode = "LEAD-1003",
                    LeadName = "Marcus Vance",
                    Email = "m.vance@vancestrategies.com",
                    Phone = "+1 206-555-0198",
                    CompanyName = "Vance Strategies",
                    Source = "Campaign",
                    Status = "Qualified",
                    Priority = "High",
                    ExpectedValue = 60000,
                    CreatedDate = DateTime.UtcNow.AddDays(-5),
                    AssignedTo = salesUser.Id,
                    Notes = "Budget approved, evaluating options."
                };

                var lead4 = new Lead
                {
                    LeadCode = "LEAD-1004",
                    LeadName = "Rachel Green",
                    Email = "rachel.g@greenlogistics.com",
                    Phone = "+1 512-555-0112",
                    CompanyName = "Green Logistics",
                    Source = "Cold Call",
                    Status = "Converted",
                    Priority = "Low",
                    ExpectedValue = 18000,
                    CreatedDate = DateTime.UtcNow.AddDays(-40),
                    AssignedTo = salesUser.Id,
                    ConvertedCustomerId = cust1.CustomerId,
                    Notes = "Converted into Apex Tech account."
                };

                var lead5 = new Lead
                {
                    LeadCode = "LEAD-1005",
                    LeadName = "Thomas Wright",
                    Email = "twright@wrightretail.com",
                    Phone = "+1 773-555-0144",
                    CompanyName = "Wright Retail",
                    Source = "Other",
                    Status = "Lost",
                    Priority = "Low",
                    ExpectedValue = 12000,
                    CreatedDate = DateTime.UtcNow.AddDays(-30),
                    AssignedTo = managerUser.Id,
                    Notes = "Decided to keep legacy tools."
                };

                context.Leads.AddRange(lead1, lead2, lead3, lead4, lead5);
                await context.SaveChangesAsync();

                // Seed Opportunities
                var opp1 = new Opportunity
                {
                    OpportunityName = "Apex Cloud Migration Contract",
                    CustomerId = cust1.CustomerId,
                    LeadId = lead4.LeadId,
                    Amount = 55000,
                    Stage = "Negotiation",
                    Probability = 80,
                    ExpectedCloseDate = DateTime.Today.AddDays(20),
                    Status = "Open",
                    AssignedTo = salesUser.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-25),
                    Notes = "Contract review with legal department."
                };

                var opp2 = new Opportunity
                {
                    OpportunityName = "BlueStar Security Suite License",
                    CustomerId = cust2.CustomerId,
                    Amount = 32000,
                    Stage = "Proposal",
                    Probability = 50,
                    ExpectedCloseDate = DateTime.Today.AddDays(45),
                    Status = "Open",
                    AssignedTo = salesUser.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-15),
                    Notes = "Proposal delivered, awaiting stakeholder feedback."
                };

                var opp3 = new Opportunity
                {
                    OpportunityName = "Zenith Annual Analytics Platform",
                    CustomerId = cust3.CustomerId,
                    Amount = 75000,
                    Stage = "Won",
                    Probability = 100,
                    ExpectedCloseDate = DateTime.Today.AddDays(-5),
                    Status = "Won",
                    AssignedTo = managerUser.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-50),
                    Notes = "Closed won deal for 1 year enterprise license."
                };

                var opp4 = new Opportunity
                {
                    OpportunityName = "Apex Additional Seat Expansion",
                    CustomerId = cust1.CustomerId,
                    Amount = 15000,
                    Stage = "Qualification",
                    Probability = 20,
                    ExpectedCloseDate = DateTime.Today.AddDays(60),
                    Status = "Open",
                    AssignedTo = salesUser.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-2),
                    Notes = "Assessing department expansion requirements."
                };

                context.Opportunities.AddRange(opp1, opp2, opp3, opp4);
                await context.SaveChangesAsync();

                // Seed Follow-ups
                var fu1 = new FollowUp
                {
                    CustomerId = cust1.CustomerId,
                    Subject = "Legal Terms Review Call",
                    FollowUpDate = DateTime.Today.AddDays(2),
                    FollowUpType = "Call",
                    Status = "Planned",
                    Remarks = "Follow up regarding Section 4 liability clause.",
                    AssignedTo = salesUser.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-3)
                };

                var fu2 = new FollowUp
                {
                    LeadId = lead3.LeadId,
                    Subject = "Platform Feature Demo",
                    FollowUpDate = DateTime.Today.AddDays(4),
                    FollowUpType = "Meeting",
                    Status = "Planned",
                    Remarks = "Demonstrate reporting and export capabilities.",
                    AssignedTo = salesUser.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-1)
                };

                var fu3 = new FollowUp
                {
                    CustomerId = cust2.CustomerId,
                    Subject = "Quarterly Business Review",
                    FollowUpDate = DateTime.Today.AddDays(-10),
                    FollowUpType = "Meeting",
                    Status = "Completed",
                    Remarks = "QBR completed with VP of Operations.",
                    AssignedTo = salesUser.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-20)
                };

                context.FollowUps.AddRange(fu1, fu2, fu3);
                await context.SaveChangesAsync();

                // Seed Activities
                var act1 = new Activity
                {
                    ActivityType = "Meeting",
                    Subject = "Initial Architecture Briefing",
                    Description = "Presented architecture overview to Apex CTO.",
                    ActivityDate = DateTime.UtcNow.AddDays(-25),
                    CustomerId = cust1.CustomerId,
                    AssignedTo = salesUser.Id,
                    Status = "Completed"
                };

                var act2 = new Activity
                {
                    ActivityType = "Email",
                    Subject = "Sent Pricing Breakdown Proposal",
                    Description = "Sent tier breakdown and SLA agreements.",
                    ActivityDate = DateTime.UtcNow.AddDays(-14),
                    CustomerId = cust2.CustomerId,
                    AssignedTo = salesUser.Id,
                    Status = "Completed"
                };

                var act3 = new Activity
                {
                    ActivityType = "Call",
                    Subject = "Introductory Discovery Call",
                    Description = "Spoke with Marcus Vance about project scope and timeline.",
                    ActivityDate = DateTime.UtcNow.AddDays(-5),
                    LeadId = lead3.LeadId,
                    AssignedTo = salesUser.Id,
                    Status = "Completed"
                };

                context.Activities.AddRange(act1, act2, act3);
                await context.SaveChangesAsync();

                // Seed Initial Audit Logs
                var auditEntries = new[]
                {
                    new AuditLog
                    {
                        UserId = adminUser.Id,
                        UserName = adminUser.FullName,
                        Action = "Login",
                        EntityName = "Auth",
                        Details = "Administrator logged into AcxiomCRM",
                        Result = "Success",
                        IpAddress = "127.0.0.1",
                        CreatedDate = DateTime.UtcNow.AddDays(-1)
                    },
                    new AuditLog
                    {
                        UserId = adminUser.Id,
                        UserName = adminUser.FullName,
                        Action = "Create",
                        EntityName = "Customer",
                        RecordId = cust1.CustomerId.ToString(),
                        NewValue = cust1.CustomerName,
                        Details = "Customer record created",
                        Result = "Success",
                        IpAddress = "127.0.0.1",
                        CreatedDate = DateTime.UtcNow.AddDays(-60)
                    },
                    new AuditLog
                    {
                        UserId = salesUser.Id,
                        UserName = salesUser.FullName,
                        Action = "Create",
                        EntityName = "Opportunity",
                        RecordId = opp1.OpportunityId.ToString(),
                        NewValue = opp1.OpportunityName,
                        Details = "Opportunity created with Amount $55,000",
                        Result = "Success",
                        IpAddress = "127.0.0.1",
                        CreatedDate = DateTime.UtcNow.AddDays(-25)
                    }
                };

                context.AuditLogs.AddRange(auditEntries);
                await context.SaveChangesAsync();
            }
        }
    }
}
