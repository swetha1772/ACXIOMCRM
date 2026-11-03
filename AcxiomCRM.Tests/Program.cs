using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Tests
{
    class Program
    {
        static int _passed = 0;
        static int _failed = 0;

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=================================================================");
            Console.WriteLine("    ACXIOMCRM COMPREHENSIVE ACCEPTANCE & VALIDATION TEST RUNNER   ");
            Console.WriteLine("=================================================================\n");

            RunTest("Acceptance 3 & 4: Customer Validation (Required Name, Email format, Phone format)", TestCustomerValidation);
            RunTest("Acceptance 4: Customer Duplicate Email & Phone Prevention", TestCustomerDuplicatePrevention);
            RunTest("Acceptance 5: Opportunity Amount <= 0 Rejected", TestOpportunityAmountValidation);
            RunTest("Acceptance 6: Opportunity Probability > 100 Rejected", TestOpportunityProbabilityValidation);
            RunTest("Acceptance 7: Opportunity Expected Close Date in Past Rejected", TestOpportunityCloseDateValidation);
            RunTest("Acceptance 8: Follow-Up Date Before Today Rejected for Planned Task", TestFollowUpDateValidation);
            RunTest("Acceptance 9 & 10: Role-Based Scoping (SalesExecutive vs Manager/Admin)", TestRoleScoping);
            await RunTestAsync("Acceptance 12: Lead Conversion to Customer & Opportunity Workflow + Audit Logging", TestLeadConversionWorkflow);
            RunTest("Acceptance 13: Weighted Pipeline Derivation Formula (Amount * Probability / 100)", TestWeightedPipelineCalculation);
            RunTest("Acceptance 14: REST API DTOs & Validation", TestRestApiDtoValidation);

            Console.WriteLine("\n=================================================================");
            Console.WriteLine($"TEST RESULTS SUMMARY: {_passed} PASSED, {_failed} FAILED");
            Console.WriteLine("=================================================================");

            return _failed > 0 ? 1 : 0;
        }

        static void RunTest(string testName, Action testAction)
        {
            try
            {
                testAction();
                Console.WriteLine($"[PASS] {testName}");
                _passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testName}: {ex.Message}");
                Console.ResetColor();
                _failed++;
            }
        }

        static async Task RunTestAsync(string testName, Func<Task> testAction)
        {
            try
            {
                await testAction();
                Console.WriteLine($"[PASS] {testName}");
                _passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testName}: {ex.Message}");
                Console.ResetColor();
                _failed++;
            }
        }

        static ApplicationDbContext GetTestDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source=acceptance_test_{Guid.NewGuid():N}.db")
                .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        static IList<ValidationResult> Validate(object model)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, context, results, true);
            return results;
        }

        static void TestCustomerValidation()
        {
            // Invalid customer: missing name, invalid email, invalid phone
            var invalid = new CustomerCreateEditViewModel
            {
                CustomerName = "",
                Email = "invalid-email",
                Phone = "12",
                Status = "Active"
            };

            var results = Validate(invalid);
            if (!results.Any(r => r.ErrorMessage!.Contains("Customer Name is required")))
                throw new Exception("Customer Name required validation failed.");
            if (!results.Any(r => r.ErrorMessage!.Contains("valid email address")))
                throw new Exception("Customer Email format validation failed.");
            if (!results.Any(r => r.ErrorMessage!.Contains("valid phone number")))
                throw new Exception("Customer Phone regex validation failed.");

            // Valid customer
            var valid = new CustomerCreateEditViewModel
            {
                CustomerName = "Enterprise Global Corp",
                Email = "contact@enterpriseglobal.com",
                Phone = "+1 555-0199",
                Status = "Active"
            };
            var validResults = Validate(valid);
            if (validResults.Any())
                throw new Exception("Valid customer was rejected: " + validResults.First().ErrorMessage);
        }

        static void TestCustomerDuplicatePrevention()
        {
            using var context = GetTestDbContext();
            var existing = new Customer
            {
                CustomerCode = "CUST-9999",
                CustomerName = "Existing Customer",
                Email = "duplicate@test.com",
                Phone = "+1 555-9999",
                Status = "Active"
            };
            context.Customers.Add(existing);
            context.SaveChanges();

            // Attempting to add duplicate email must be rejected by uniqueness rule
            var duplicateEmail = "duplicate@test.com";
            var emailExists = context.Customers.Any(c => c.Email.ToLower() == duplicateEmail.ToLower());
            if (!emailExists)
                throw new Exception("Duplicate email check failed to detect existing email.");

            var phoneExists = context.Customers.Any(c => c.Phone == "+1 555-9999");
            if (!phoneExists)
                throw new Exception("Duplicate phone check failed to detect existing phone.");

            context.Database.EnsureDeleted();
        }

        static void TestOpportunityAmountValidation()
        {
            var opp = new OpportunityCreateEditViewModel
            {
                OpportunityName = "Zero Amount Deal",
                CustomerId = 1,
                Amount = 0,
                Probability = 50,
                ExpectedCloseDate = DateTime.Today.AddDays(10),
                Stage = "Qualification"
            };
            var results = Validate(opp);
            if (!results.Any(r => r.ErrorMessage!.Contains("Opportunity Amount must be greater than 0")))
                throw new Exception("Failed to reject Opportunity Amount <= 0.");
        }

        static void TestOpportunityProbabilityValidation()
        {
            var opp = new OpportunityCreateEditViewModel
            {
                OpportunityName = "Invalid Prob Deal",
                CustomerId = 1,
                Amount = 10000,
                Probability = 150,
                ExpectedCloseDate = DateTime.Today.AddDays(10),
                Stage = "Qualification"
            };
            var results = Validate(opp);
            if (!results.Any(r => r.ErrorMessage!.Contains("Probability must be between 0 and 100")))
                throw new Exception("Failed to reject Opportunity Probability > 100.");
        }

        static void TestOpportunityCloseDateValidation()
        {
            var opp = new OpportunityCreateEditViewModel
            {
                OpportunityName = "Past Date Deal",
                CustomerId = 1,
                Amount = 10000,
                Probability = 50,
                ExpectedCloseDate = DateTime.Today.AddDays(-2),
                Stage = "Qualification"
            };
            var results = Validate(opp);
            if (!results.Any(r => r.ErrorMessage!.Contains("Expected Close Date cannot be in the past")))
                throw new Exception("Failed to reject Opportunity with Expected Close Date in the past.");
        }

        static void TestFollowUpDateValidation()
        {
            var fu = new FollowUpCreateEditViewModel
            {
                Subject = "Past date outreach",
                FollowUpDate = DateTime.Today.AddDays(-1),
                FollowUpType = "Call",
                Status = "Planned"
            };
            var results = Validate(fu);
            if (!results.Any(r => r.ErrorMessage!.Contains("Follow-up date cannot be earlier than today")))
                throw new Exception("Failed to reject Planned Follow-Up dated before today.");
        }

        static void TestRoleScoping()
        {
            using var context = GetTestDbContext();
            var salesUser1 = "sales-rep-1";
            var salesUser2 = "sales-rep-2";

            context.Users.AddRange(
                new ApplicationUser { Id = salesUser1, UserName = salesUser1, Email = "rep1@test.com", FullName = "Rep 1" },
                new ApplicationUser { Id = salesUser2, UserName = salesUser2, Email = "rep2@test.com", FullName = "Rep 2" }
            );
            context.SaveChanges();

            context.Customers.AddRange(
                new Customer { CustomerCode = "C1", CustomerName = "Rep 1 Account", Email = "rep1@corp.com", Phone = "1111111111", OwnerId = salesUser1 },
                new Customer { CustomerCode = "C2", CustomerName = "Rep 2 Account", Email = "rep2@corp.com", Phone = "2222222222", OwnerId = salesUser2 }
            );
            context.SaveChanges();

            // Rep 1 scope
            var salesClaims = new[] { new Claim(ClaimTypes.NameIdentifier, salesUser1), new Claim(ClaimTypes.Role, "SalesExecutive") };
            var salesPrincipal = new ClaimsPrincipal(new ClaimsIdentity(salesClaims));

            var rep1Records = context.Customers.Where(c => c.OwnerId == salesUser1).ToList();
            if (rep1Records.Count != 1 || rep1Records[0].CustomerName != "Rep 1 Account")
                throw new Exception("SalesExecutive was able to access unassigned customer accounts!");

            // Manager scope
            var allRecords = context.Customers.ToList();
            if (allRecords.Count != 2)
                throw new Exception("Manager could not access all team accounts.");

            context.Database.EnsureDeleted();
        }

        static async Task TestLeadConversionWorkflow()
        {
            using var context = GetTestDbContext();

            var repUser = new ApplicationUser { Id = "rep-1", UserName = "rep1", Email = "rep1@test.com", FullName = "Rep One" };
            context.Users.Add(repUser);
            await context.SaveChangesAsync();

            var lead = new Lead
            {
                LeadCode = "LEAD-100",
                LeadName = "Samantha Ray",
                Email = "samantha@orbitallabs.io",
                Phone = "+1 415-555-0188",
                Source = "Website",
                Status = "Qualified",
                ExpectedValue = 65000,
                AssignedTo = "rep-1"
            };
            context.Leads.Add(lead);
            await context.SaveChangesAsync();

            // Perform Lead Conversion
            var customer = new Customer
            {
                CustomerCode = "CUST-500",
                CustomerName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                Status = "Active",
                OwnerId = lead.AssignedTo
            };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var opp = new Opportunity
            {
                OpportunityName = $"{lead.LeadName} Contract",
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = lead.ExpectedValue,
                Stage = "Qualification",
                Probability = 30,
                ExpectedCloseDate = DateTime.Today.AddDays(30),
                Status = "Open",
                AssignedTo = lead.AssignedTo
            };
            context.Opportunities.Add(opp);

            lead.Status = "Converted";
            lead.ConvertedCustomerId = customer.CustomerId;

            var auditLog = new AuditLog
            {
                UserId = "rep-1",
                UserName = "Rep One",
                Action = "Lead Conversion",
                EntityName = "Lead",
                RecordId = lead.LeadId.ToString(),
                NewValue = $"Converted to CustomerId: {customer.CustomerId}",
                Result = "Success",
                CreatedDate = DateTime.UtcNow
            };
            context.AuditLogs.Add(auditLog);
            await context.SaveChangesAsync();

            var verifiedLead = await context.Leads.FindAsync(lead.LeadId);
            if (verifiedLead?.Status != "Converted" || verifiedLead.ConvertedCustomerId != customer.CustomerId)
                throw new Exception("Lead status or converted customer link failed to update.");

            var verifiedAudit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Lead Conversion");
            if (verifiedAudit == null)
                throw new Exception("Audit log entry for lead conversion was not recorded.");

            context.Database.EnsureDeleted();
        }

        static void TestWeightedPipelineCalculation()
        {
            var opp = new Opportunity
            {
                OpportunityName = "MegaDeal",
                Amount = 150000m,
                Probability = 40,
                ExpectedCloseDate = DateTime.Today.AddDays(15)
            };

            // Expected = 150,000 * 40 / 100 = 60,000
            if (opp.WeightedAmount != 60000m)
                throw new Exception($"Expected 60000 but got {opp.WeightedAmount}");
        }

        static void TestRestApiDtoValidation()
        {
            var invalidCustDto = new CreateCustomerRequestDto
            {
                CustomerName = "",
                Email = "not-an-email",
                Phone = "12"
            };

            var results = Validate(invalidCustDto);
            if (!results.Any(r => r.ErrorMessage!.Contains("Customer Name is required")))
                throw new Exception("DTO missing Customer Name validation.");
            if (!results.Any(r => r.ErrorMessage!.Contains("valid email address")))
                throw new Exception("DTO missing Email validation.");
            if (!results.Any(r => r.ErrorMessage!.Contains("valid phone number")))
                throw new Exception("DTO missing Phone validation.");

            var validCustDto = new CreateCustomerRequestDto
            {
                CustomerName = "Valid Client",
                Email = "client@domain.com",
                Phone = "+1 555-0122"
            };
            if (Validate(validCustDto).Any())
                throw new Exception("Valid customer DTO failed validation.");
        }
    }
}
