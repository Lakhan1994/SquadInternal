using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SquadInternal.Models
{
    public class Employee
    {
        // ==========================
        // PRIMARY KEY
        // ==========================

        public int Id { get; set; }   

        // ==========================
        // FOREIGN KEYS
        // ==========================

        [Required]
        public int UserId { get; set; }

        public int? AddedBy { get; set; }

        // ==========================
        // BASIC DETAILS
        // ==========================

        [Required, StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfJoining { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        // ==========================
        // EMPLOYMENT DETAILS
        // ==========================

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Salary { get; set; }

        [StringLength(300)]
        public string? AppointmentLetterPath { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        public DateTime AddedDate { get; set; } = DateTime.Now;

        // ==========================
        // NAVIGATION PROPERTIES
        // ==========================

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(AddedBy))]
        public User? AddedByUser { get; set; }

        public ICollection<EmployeeEducation> Educations { get; set; }
            = new List<EmployeeEducation>();

        public int? ReportingToUserId { get; set; }

        public User? ReportingToUser { get; set; }

    }
}
