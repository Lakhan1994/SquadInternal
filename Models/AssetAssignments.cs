using SquadInternal.Models.Enum;

namespace SquadInternal.Models
{
    public class AssetAssignment
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public Asset? Asset { get; set; }

        public int EmployeeId { get; set; }

        public Employee? Employee { get; set; }

        // -------------------------
        // Assignment
        // -------------------------

        public DateTime AssignedDate { get; set; }

        public int AssignedBy { get; set; }

        // -------------------------
        // Return
        // -------------------------

        public DateTime? ReturnedDate { get; set; }

        public int? ReturnedBy { get; set; }

        // -------------------------
        // Condition
        // -------------------------

        public AssetCondition ConditionAtAssignment { get; set; }

        public AssetCondition? ConditionAtReturn { get; set; }

        // -------------------------
        // Handover
        // -------------------------

        public string? HandoverRemarks { get; set; }

        public string? ReturnRemarks { get; set; }

        public string? HandoverDocumentPath { get; set; }

        public string? ReturnDocumentPath { get; set; }

        // -------------------------
        // Current
        // -------------------------

        public bool IsCurrent { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
