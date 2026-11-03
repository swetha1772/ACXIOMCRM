using System;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Attributes;

namespace AcxiomCRM.Models
{
    public class FollowUp
    {
        public int FollowUpId { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? LeadId { get; set; }
        public Lead? Lead { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Follow-up Date is required.")]
        [DataType(DataType.Date)]
        [NotInPastDate(ErrorMessage = "Follow-up date cannot be earlier than today.")]
        public DateTime FollowUpDate { get; set; }

        [Required(ErrorMessage = "Follow-up Type is required.")]
        [StringLength(50)]
        public string FollowUpType { get; set; } = "Call"; // Call, Meeting, Email, Demo, Other

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Planned"; // Planned, Completed, Missed, Cancelled

        [StringLength(500)]
        public string? Remarks { get; set; }

        public string? AssignedTo { get; set; }
        public ApplicationUser? AssignedUser { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
