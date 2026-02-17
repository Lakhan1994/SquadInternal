using System;
using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models
{
    public class EmployeeLeave
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        public Employee? Employee { get; set; }

        [Required]
        public DateTime FromDate { get; set; }

        [Required]
        public DateTime ToDate { get; set; }

        [Required]
        public string LeaveType { get; set; } = "Casual";

        public string? Reason { get; set; }

        public string Status { get; set; } = "Pending";
        // Pending / Approved / Rejected

        public DateTime AppliedOn { get; set; } = DateTime.Now;

        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
    }
}
