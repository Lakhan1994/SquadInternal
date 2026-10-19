using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models
{
    public class AnnualLeavePolicyAudit
    {
        public long Id { get; set; }
        public int AnnualLeavePolicyId { get; set; }
        public int ChangedByUserId { get; set; }
        public DateTime ChangedOn { get; set; } = DateTime.UtcNow;

        [Required, StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string SnapshotJson { get; set; } = string.Empty;

        public AnnualLeavePolicy? Policy { get; set; }
    }
}