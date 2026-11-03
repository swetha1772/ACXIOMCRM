using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels
{
    public class LeadListViewModel
    {
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public string? AssignedUserFilter { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public List<Lead> Leads { get; set; } = new();
        public SelectList? UsersList { get; set; }
    }

    public class LeadCreateEditViewModel
    {
        public int LeadId { get; set; }
        public string? LeadCode { get; set; }

        [Required(ErrorMessage = "Lead Name is required.")]
        [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
        [Display(Name = "Lead Name")]
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
        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "Lead Source is required.")]
        [StringLength(50)]
        public string Source { get; set; } = "Website";

        [Required(ErrorMessage = "Lead Status is mandatory.")]
        [StringLength(30)]
        public string Status { get; set; } = "New";

        [StringLength(20)]
        public string Priority { get; set; } = "Medium";

        [Range(0, 1000000000, ErrorMessage = "Expected Value must be greater than or equal to 0.")]
        [Display(Name = "Expected Value ($)")]
        public decimal ExpectedValue { get; set; } = 0;

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public SelectList? UsersList { get; set; }
    }

    public class LeadConvertViewModel
    {
        public int LeadId { get; set; }
        public string LeadName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }

        public bool CreateOpportunity { get; set; } = true;

        [Display(Name = "Opportunity Name")]
        public string? OpportunityName { get; set; }

        [Display(Name = "Opportunity Amount ($)")]
        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        public decimal Amount { get; set; } = 1000;

        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        public int Probability { get; set; } = 25;

        [DataType(DataType.Date)]
        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

        [Display(Name = "Pipeline Stage")]
        public string Stage { get; set; } = "Qualification";
    }
}
