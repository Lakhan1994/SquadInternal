using System.ComponentModel.DataAnnotations.Schema;

namespace SquadInternal.Models
{
    public class EmployeeLeaveBalance
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int LeaveYear { get; set; }
        public string LeaveType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(9,2)")]
        public decimal OpeningBalance { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal AnnualEntitlement { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal ProratedEntitlement { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal BonusCredits { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal ApprovedConsumption { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal Adjustments { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal ExpiredBalance { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal ClosingBalance { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;
        public Employee? Employee { get; set; }
    }
}