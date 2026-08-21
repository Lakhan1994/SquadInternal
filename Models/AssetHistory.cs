using SquadInternal.Models.Enum;

namespace SquadInternal.Models
{
    public class AssetHistory
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public Asset? Asset { get; set; }

        public string Action { get; set; } = string.Empty;

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public string? Remarks { get; set; }

        public int PerformedBy { get; set; }

        public DateTime PerformedDate { get; set; } = DateTime.Now;
    }
}
