using System;

namespace SquadInternal.Models.ViewModels
{
    public class MonthlySummaryRowVM
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int TotalDaysInMonth { get; set; }
        public double TotalLeavesTaken { get; set; }
        public double TotalPaidDays { get; set; }
    }
}
