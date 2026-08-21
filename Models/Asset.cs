using SquadInternal.Models.Enum;

namespace SquadInternal.Models
{
    public class Asset
    {
        public int Id { get; set; }

        // -------------------------
        // Identification
        // -------------------------

        public string AssetCode { get; set; } = string.Empty;

        public int AssetCategoryId { get; set; }

        public AssetCategory? AssetCategory { get; set; }

        public string AssetName { get; set; } = string.Empty;

        public string? Brand { get; set; }

        public string? Model { get; set; }

        public string? SerialNumber { get; set; }

        // -------------------------
        // Purchase Details
        // -------------------------

        public string? VendorName { get; set; }

        public string? InvoiceNumber { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public decimal? PurchaseCost { get; set; }

        public DateTime? WarrantyStartDate { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        // -------------------------
        // Asset Details
        // -------------------------

        public string? Configuration { get; set; }

        public string? Description { get; set; }

        public string? Location { get; set; }

        public AssetCondition Condition { get; set; }
            = AssetCondition.New;

        public AssetStatus Status { get; set; }
            = AssetStatus.Available;

        // -------------------------
        // Current Assignment
        // -------------------------

        public int? CurrentEmployeeId { get; set; }

        public Employee? CurrentEmployee { get; set; }

        // -------------------------
        // Additional
        // -------------------------

        public string? AssetImagePath { get; set; }

        public string? InvoiceFilePath { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        public int AddedBy { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; }

        public ICollection<AssetAssignment> Assignments { get; set; }
            = new List<AssetAssignment>();

        public ICollection<AssetHistory> History { get; set; }
            = new List<AssetHistory>();
    }
}
