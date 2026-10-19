using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SquadInternal.Models
{
    public class LeaveBalanceTransaction
    {
        public long Id { get; set; }
        public int EmployeeId { get; set; }
        public int LeaveYear { get; set; }

        [Required, StringLength(30)]
        public string LeaveType { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string TransactionType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(9,2)")]
        public decimal Days { get; set; }

        public int? EmployeeLeaveId { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresOn { get; set; }

        [Required, StringLength(80)]
        public string IdempotencyKey { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Reason { get; set; }

        public Employee? Employee { get; set; }
        public EmployeeLeave? EmployeeLeave { get; set; }
    }
}