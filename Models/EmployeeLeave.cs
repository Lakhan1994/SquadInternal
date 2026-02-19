using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SquadInternal.Models
{
    public class EmployeeLeave : IValidatableObject
    {
        // ==========================
        // PRIMARY KEY
        // ==========================
        public int Id { get; set; }

        // ==========================
        // FOREIGN KEY
        // ==========================
        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        // ==========================
        // LEAVE DETAILS
        // ==========================

        [Required]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        [Required]
        [StringLength(30)]
        public string LeaveType { get; set; } = "Casual";

        [StringLength(500)]
        public string? Reason { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";
        // Pending / Approved / Rejected

        public DateTime AppliedOn { get; set; } = DateTime.UtcNow;

        // ==========================
        // APPROVAL
        // ==========================
        public int? ApprovedByUserId { get; set; }

        [ForeignKey(nameof(ApprovedByUserId))]
        public User? ApprovedByUser { get; set; }

        // ==========================
        // CUSTOM VALIDATION
        // ==========================
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ToDate < FromDate)
            {
                yield return new ValidationResult(
                    "To Date cannot be earlier than From Date.",
                    new[] { nameof(ToDate) });
            }

            if (FromDate.Date < DateTime.Today)
            {
                yield return new ValidationResult(
                    "Leave cannot be applied for past dates.",
                    new[] { nameof(FromDate) });
            }
        }
    }
}
