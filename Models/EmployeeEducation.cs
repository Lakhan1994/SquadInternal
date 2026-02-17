using System;
using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models
{
    public class EmployeeEducation
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }    
        public Employee Employee { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Level { get; set; } = "";    

        [MaxLength(120)]
        public string? Institute { get; set; }

        [MaxLength(120)]
        public string? BoardOrUniversity { get; set; }

        [MaxLength(60)]
        public string? Stream { get; set; }

        public int? PassingYear { get; set; }

        [MaxLength(20)]
        public string? PercentageOrCGPA { get; set; }
    }
}
