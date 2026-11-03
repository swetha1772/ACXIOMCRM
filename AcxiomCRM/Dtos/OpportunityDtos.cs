using System;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Attributes;

namespace AcxiomCRM.Dtos
{
    public class OpportunityResponseDto
    {
        public int OpportunityId { get; set; }
        public string OpportunityName { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? LeadId { get; set; }
        public decimal Amount { get; set; }
        public string Stage { get; set; } = string.Empty;
        public int Probability { get; set; }
        public decimal WeightedAmount { get; set; }
        public DateTime ExpectedCloseDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? AssignedUserName { get; set; }
    }

    public class CreateOpportunityRequestDto
    {
        [Required(ErrorMessage = "Opportunity Name is required.")]
        [StringLength(100, ErrorMessage = "Opportunity Name cannot exceed 100 characters.")]
        public string OpportunityName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer ID is required.")]
        public int CustomerId { get; set; }

        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Opportunity Amount is required.")]
        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Stage is required.")]
        [StringLength(30)]
        public string Stage { get; set; } = "Qualification";

        [Required(ErrorMessage = "Probability is required.")]
        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        public int Probability { get; set; } = 25;

        [Required(ErrorMessage = "Expected Close Date is required.")]
        [NotInPastDate(ErrorMessage = "Expected Close Date cannot be in the past.")]
        public DateTime ExpectedCloseDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class UpdateOpportunityRequestDto : CreateOpportunityRequestDto
    {
    }
}
