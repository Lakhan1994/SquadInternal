using System;

namespace SquadInternal.Models
{
    public class EmployeeDocument
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public string DocumentType { get; set; } = "";
        public string DocumentName { get; set; } = "";
        public string FilePath { get; set; } = "";

        public DateTime UploadedDate { get; set; } = DateTime.Now;

        // Navigation property
        public Employee Employee { get; set; }
    }
}
