using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Attributes;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels
{
    public class FollowUpListViewModel
    {
        public string? StatusFilter { get; set; }
        public string? TypeFilter { get; set; }
        public DateTime? DateFilter { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public List<FollowUp> FollowUps { get; set; } = new();
    }

    public class FollowUpCreateEditViewModel
    {
        public int FollowUpId { get; set; }

        [Display(Name = "Related Customer (Optional)")]
        public int? CustomerId { get; set; }

        [Display(Name = "Related Lead (Optional)")]
        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Follow-up Date is required.")]
        [DataType(DataType.Date)]
        [NotInPastDate(ErrorMessage = "Follow-up date cannot be earlier than today.")]
        [Display(Name = "Follow-Up Date")]
        public DateTime FollowUpDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Follow-up Type is required.")]
        [Display(Name = "Follow-Up Type")]
        public string FollowUpType { get; set; } = "Call";

        [Required]
        public string Status { get; set; } = "Planned";

        [StringLength(500)]
        public string? Remarks { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        public SelectList? CustomersList { get; set; }
        public SelectList? LeadsList { get; set; }
        public SelectList? UsersList { get; set; }
    }
}
