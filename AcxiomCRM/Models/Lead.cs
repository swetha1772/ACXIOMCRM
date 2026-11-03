using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class Lead
    {
        public int LeadId { get; set; }

        [Required]
        [MaxLength(20)]
        public string LeadCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lead Name is required.")]
        [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
        public string LeadName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [RegularExpression(@"^[0-9+\-\(\)\s]{7,15}$", ErrorMessage = "Enter a valid phone number.")]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(100)]
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "Lead Source is required.")]
        [StringLength(50)]
        public string Source { get; set; } = "Website"; // Website, Referral, Cold Call, Campaign, Partner, Other

        [Required(ErrorMessage = "Lead Status is mandatory.")]
        [StringLength(30)]
        public string Status { get; set; } = "New"; // New, Contacted, Qualified, Unqualified, Converted, Lost

        [StringLength(20)]
        public string Priority { get; set; } = "Medium"; // High, Medium, Low

        [Range(0, 1000000000, ErrorMessage = "Expected Value must be greater than or equal to 0.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedValue { get; set; } = 0;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public string? AssignedTo { get; set; }
        public ApplicationUser? AssignedUser { get; set; }

        public int? ConvertedCustomerId { get; set; }
        public Customer? ConvertedCustomer { get; set; }

        public string? Notes { get; set; }

        // Navigation
        public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
