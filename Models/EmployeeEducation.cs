using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SquadInternal.Models
{
    public class EmployeeEducation
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

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        // ==========================
        // EDUCATION DETAILS
        // ==========================
        [Required]
        [StringLength(50)]
        public string Level { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Institute { get; set; }

        [StringLength(150)]
        public string? BoardOrUniversity { get; set; }

        [StringLength(100)]
        public string? Stream { get; set; }

        [Range(1900, 2100, ErrorMessage = "Invalid passing year.")]
        public int? PassingYear { get; set; }

        [StringLength(20)]
        public string? PercentageOrCGPA { get; set; }
    }
}
