using Microsoft.AspNetCore.Http;
using SquadInternal.Models.Enum;
using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models.ViewModels
{
    public class CreateAssetVM
    {
        [Required]
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

        // CHANGE THIS: Use string instead of enum
        public string Condition { get; set; } = "New";

        public IFormFile? AssetImage { get; set; }
        public IFormFile? InvoiceFile { get; set; }
    }
}