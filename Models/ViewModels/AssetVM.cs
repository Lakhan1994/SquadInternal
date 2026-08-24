using Microsoft.AspNetCore.Http;
using SquadInternal.Models.Enum;
using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models.ViewModels
{
    public class AssetVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Asset code is required.")]
        public string AssetCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select an asset category.")]
        public int AssetCategoryId { get; set; }

        [Required(ErrorMessage = "Asset name is required.")]
        public string AssetName { get; set; } = string.Empty;

        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? VendorName { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchaseCost { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public string? Configuration { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }

        // keep string for binding to select list values
        public string Condition { get; set; } = AssetCondition.New.ToString();

        public IFormFile? AssetImage { get; set; }
        public IFormFile? InvoiceFile { get; set; }
        // Additional state for edit scenarios
        public bool IsActive { get; set; } = true;
        public AssetStatus Status { get; set; } = AssetStatus.Available;
        public int? CurrentEmployeeId { get; set; }
    }
}
