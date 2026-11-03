using System;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Attributes;

namespace AcxiomCRM.Dtos
{
    public class FollowUpResponseDto
    {
        public int FollowUpId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? LeadId { get; set; }
        public string? LeadName { get; set; }
        public string Subject { get; set; } = string.Empty;
        public DateTime FollowUpDate { get; set; }
        public string FollowUpType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public string? AssignedUserName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateFollowUpRequestDto
    {
        public int? CustomerId { get; set; }
        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Follow-up Date is required.")]
        [NotInPastDate(ErrorMessage = "Follow-up date cannot be earlier than today.")]
        public DateTime FollowUpDate { get; set; }

        [Required(ErrorMessage = "Follow-up Type is required.")]
        public string FollowUpType { get; set; } = "Call";

        public string Status { get; set; } = "Planned";

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class UpdateFollowUpRequestDto : CreateFollowUpRequestDto
    {
    }
}
