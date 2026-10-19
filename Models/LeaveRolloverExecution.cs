using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models
{
    public class LeaveRolloverExecution
    {
        public int Id { get; set; }
        public int LeaveYear { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = "Started";

        public DateTime StartedOn { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedOn { get; set; }
        public int? StartedByUserId { get; set; }

        [StringLength(2000)]
        public string? Error { get; set; }
    }
}