using System;
using System.Collections.Generic;

namespace AcxiomCRM.ViewModels
{
    public class DashboardViewModel
    {
        public string Filter { get; set; } = "all"; // all, today, thisweek, thismonth
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public int OpenOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
        public int PendingFollowUps { get; set; }

        // Chart Data
        public List<string> LeadStatusLabels { get; set; } = new();
        public List<int> LeadStatusCounts { get; set; } = new();

        public List<string> PipelineStageLabels { get; set; } = new();
        public List<decimal> PipelineStageAmounts { get; set; } = new();
        public List<int> PipelineStageCounts { get; set; } = new();

        public List<string> MonthlySalesLabels { get; set; } = new();
        public List<decimal> MonthlySalesAmounts { get; set; } = new();

        // Recent items
        public List<RecentActivityItem> RecentActivities { get; set; } = new();
        public List<UpcomingFollowUpItem> UpcomingFollowUps { get; set; } = new();
    }

    public class RecentActivityItem
    {
        public string Type { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? RelatedName { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class UpcomingFollowUpItem
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? RelatedName { get; set; }
        public bool IsOverdue { get; set; }
    }
}
