using System;
using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models
{
    public class SquadHoliday
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime HolidayDate { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }   // National / Optional / Company etc.

        public bool IsActive { get; set; } = true;   // Soft delete support
        public bool IsHalfDay { get; set; }
        public DateTime CreatedOn { get; set; }
        


    }
}
