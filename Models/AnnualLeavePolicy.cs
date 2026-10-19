using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SquadInternal.Models
{
    public class AnnualLeavePolicy
    {
        public int Id { get; set; }

        [Required]
        public DateTime EffectiveFrom { get; set; } = new(2027, 1, 1);

        [Column(TypeName = "decimal(9,2)")]
        public decimal CasualLeaveDays { get; set; } = 3m;

        [Column(TypeName = "decimal(9,2)")]
        public decimal SickLeaveDays { get; set; } = 3m;

        public bool EarnedLeaveEnabled { get; set; } = true;

        [Column(TypeName = "decimal(9,2)")]
        public decimal EarnedLeaveDays { get; set; } = 6m;

        public bool ProrateNewJoiners { get; set; } = true;
        public bool CasualLeaveBonusEligible { get; set; }
        public bool SickLeaveBonusEligible { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal BonusConversionRate { get; set; } = 1m;

        [Column(TypeName = "decimal(9,2)")]
        public decimal? BonusMaxEligibleUnusedDays { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        public decimal? BonusMaxDaysPerEmployeeYear { get; set; }

        public bool BonusUsesSeparateCategory { get; set; }
        public int? BonusExpirationMonths { get; set; }
        public bool BonusRequiresApproval { get; set; }
        public bool CarryForwardUnusedLeave { get; set; }

        [StringLength(100)]
        public string BusinessTimeZoneId { get; set; } = "UTC";

        public int? UpdatedByUserId { get; set; }
        public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? ChangeReason { get; set; }
    }
}