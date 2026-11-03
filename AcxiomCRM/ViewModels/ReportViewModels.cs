using System;
using System.Collections.Generic;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels
{
    public class CustomerReportViewModel
    {
        public string? StatusFilter { get; set; }
        public string? OwnerFilter { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<Customer> Customers { get; set; } = new();
    }

    public class LeadReportViewModel
    {
        public string? SourceFilter { get; set; }
        public string? StatusFilter { get; set; }
        public int TotalLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public decimal ConversionRate => TotalLeads > 0 ? Math.Round((decimal)ConvertedLeads / TotalLeads * 100, 1) : 0;
        public List<Lead> Leads { get; set; } = new();
    }

    public class FollowUpReportViewModel
    {
        public string? StatusFilter { get; set; }
        public int TotalPlanned { get; set; }
        public int TotalCompleted { get; set; }
        public int TotalMissed { get; set; }
        public int TotalOverdue { get; set; }
        public List<FollowUp> FollowUps { get; set; } = new();
    }

    public class OpportunityReportViewModel
    {
        public string? StageFilter { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalWeightedAmount { get; set; }
        public List<Opportunity> Opportunities { get; set; } = new();
    }

    public class PipelineReportViewModel
    {
        public List<StageSummaryItem> StageSummaries { get; set; } = new();
        public List<OwnerSummaryItem> OwnerSummaries { get; set; } = new();
        public decimal GrandTotalPipeline { get; set; }
        public decimal GrandTotalWeighted { get; set; }
    }

    public class StageSummaryItem
    {
        public string Stage { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class OwnerSummaryItem
    {
        public string OwnerName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class UserActivityReportViewModel
    {
        public List<UserActivityItem> UserActivities { get; set; } = new();
    }

    public class UserActivityItem
    {
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int LoginsCount { get; set; }
        public int CreatedRecordsCount { get; set; }
        public int UpdatedRecordsCount { get; set; }
        public int CompletedActivitiesCount { get; set; }
    }

    public class SalesConversionReportViewModel
    {
        public int TotalLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public decimal LeadConversionRate => TotalLeads > 0 ? Math.Round((decimal)ConvertedLeads / TotalLeads * 100, 1) : 0;

        public int TotalDeals { get; set; }
        public int WonDeals { get; set; }
        public int LostDeals { get; set; }
        public decimal WinRate => TotalDeals > 0 ? Math.Round((decimal)WonDeals / TotalDeals * 100, 1) : 0;

        public decimal TotalWonRevenue { get; set; }
        public decimal TotalLostRevenue { get; set; }
        public decimal AverageDealSize => WonDeals > 0 ? Math.Round(TotalWonRevenue / WonDeals, 2) : 0;

        public List<Lead> ConvertedLeadsList { get; set; } = new();
        public List<Opportunity> ClosedOpportunitiesList { get; set; } = new();
    }

    public class AuditReportViewModel
    {
        public int TotalAuditEvents { get; set; }
        public int SuccessfulLogins { get; set; }
        public int FailedLogins { get; set; }
        public int RoleAndSecurityChanges { get; set; }
        public int CrudOperations { get; set; }
        public List<AuditLog> AuditLogs { get; set; } = new();
    }
}
