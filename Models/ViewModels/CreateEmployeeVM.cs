using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models.ViewModels
{
    // ================================
    // MAIN VIEW MODEL
    // ================================
    public class CreateEmployeeVM
    {
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public int RoleId { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfJoining { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        public int? ReportingToUserId { get; set; }

        // Education rows
        public List<EducationRowVM> Educations { get; set; } = new();

        // Document uploads
        public List<EmployeeDocumentVM> Documents { get; set; } = new();
    }

    // ================================
    // EDUCATION ROW VM
    // ================================
    public class EducationRowVM
    {
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

    // ================================
    // DOCUMENT VM
    // ================================
    public class EmployeeDocumentVM
    {
        [Required]
        [StringLength(50)]
        public string DocumentType { get; set; } = string.Empty;

        [StringLength(150)]
        public string? DocumentName { get; set; }

        public IFormFile? File { get; set; }
    }
}
