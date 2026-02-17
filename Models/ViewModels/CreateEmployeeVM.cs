using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace SquadInternal.Models.ViewModels
{
    // ✅ MAIN VIEW MODEL
    public class CreateEmployeeVM
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public DateTime? DateOfJoining { get; set; }
        public string? Gender { get; set; }

        // 🔥 ADD THIS LINE
        public int? ReportingToUserId { get; set; }

        // ✅ Education rows
        public List<EducationRowVM> Educations { get; set; } = new();

        // ✅ Document uploads (optional)
        public List<EmployeeDocumentVM> Documents { get; set; } = new();
    }


    // ✅ EDUCATION ROW VM
    public class EducationRowVM
    {
        public string Level { get; set; } = "";
        public string? Institute { get; set; }
        public string? BoardOrUniversity { get; set; }
        public string? Stream { get; set; }
        public int? PassingYear { get; set; }
        public string? PercentageOrCGPA { get; set; }
    }

    // ✅ DOCUMENT VM
    public class EmployeeDocumentVM
    {
        // "Education", "Identity", "Certification"
        public string DocumentType { get; set; } = "";

        // "10th Marksheet", "Aadhar Card", etc.
        public string? DocumentName { get; set; }

        public IFormFile? File { get; set; }
    }
}
