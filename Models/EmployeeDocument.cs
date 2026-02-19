using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SquadInternal.Models
{
    public class EmployeeDocument
    {
        // ==========================
        // PRIMARY KEY
        // ==========================
        public int Id { get; set; }

        // ==========================
        // FOREIGN KEY
        // ==========================
        [Required]
        public int EmployeeId { get; set; }

        // ==========================
        // DOCUMENT DETAILS
        // ==========================
        [Required]
        [StringLength(50)]
        public string DocumentType { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string DocumentName { get; set; } = string.Empty;

        [Required]
        [StringLength(300)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        // ==========================
        // NAVIGATION PROPERTY
        // ==========================
        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }
    }
}
