using System;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        [Required]
        [StringLength(100)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(100)]
        public string? UserName { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty; // Login, Failed Login, Logout, Create, Update, Delete, Role Change, Security

        [Required]
        [StringLength(50)]
        public string EntityName { get; set; } = string.Empty; // Auth, Customer, Lead, Opportunity, FollowUp, Activity, User

        [StringLength(50)]
        public string? RecordId { get; set; }

        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? IpAddress { get; set; }

        [StringLength(20)]
        public string Result { get; set; } = "Success"; // Success, Failed

        [StringLength(500)]
        public string? Details { get; set; }
    }
}
