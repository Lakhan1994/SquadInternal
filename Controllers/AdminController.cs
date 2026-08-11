using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Filters;
using SquadInternal.Models;
using SquadInternal.Models.ViewModels;
using SquadInternal.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;


namespace SquadInternal.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PasswordService _passwordService;

        

        // ================= SAFE HELPERS =================

        private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            if (CurrentUserId == null)
                return null;

            return await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == CurrentUserId && e.IsActive);
        }

        private async Task<bool> HasOverlappingLeaveAsync(int employeeId, DateTime from, DateTime to)
        {
            return await _db.EmployeeLeaves.AnyAsync(l =>
                l.EmployeeId == employeeId &&
                l.Status != "Rejected" &&
                l.Status != "Cancelled" &&
                from.Date <= l.ToDate.Date &&
                to.Date >= l.FromDate.Date
            );
        }

        private List<DateTime> ExpandDates(DateTime from, DateTime to)
        {
            var days = (to.Date - from.Date).Days;

            if (days < 0)
                return new List<DateTime>();

            return Enumerable.Range(0, days + 1)
                .Select(d => from.Date.AddDays(d))
                .ToList();
        }


        // ================= ADMIN DASHBOARD =================
        [SessionAuthorize(SessionAuthMode.AnyLoggedInUser)]
        public async Task<IActionResult> Dashboard()
        {
            int? roleId = HttpContext.Session.GetInt32("RoleId");
            bool isAdmin = roleId == 1;

            ViewBag.EmployeeCount = await _db.Employees.CountAsync(e => e.IsActive);
            ViewBag.RoleCount = await _db.Roles.CountAsync(r => r.IsActive);

            var holidayDates = await _db.SquadHolidays
                .Where(h => h.IsActive)
                .Select(h => h.HolidayDate.Date)
                .ToListAsync();

            ViewBag.Holidays = holidayDates;

            ViewBag.UpcomingHolidays = await _db.SquadHolidays
                .Where(h => h.IsActive && h.HolidayDate.Date >= DateTime.Today)
                .OrderBy(h => h.HolidayDate)
                .Take(5)
                .ToListAsync();

            ViewBag.Today = DateTime.Today;

            var employee = await GetCurrentEmployeeAsync();

            if (isAdmin)
            {
                ViewBag.PendingLeaveCount = await _db.EmployeeLeaves
                    .CountAsync(l => l.Status == "Pending");
            }

            if (employee != null)
            {
                var approvedLeaves = await _db.EmployeeLeaves
                    .Where(l => l.EmployeeId == employee.Id && l.Status == "Approved")
                    .ToListAsync();

                ViewBag.ApprovedLeaveDates = approvedLeaves
                    .SelectMany(l => ExpandDates(l.FromDate, l.ToDate))
                    .ToList();

                ViewBag.MyPendingLeaveCount = await _db.EmployeeLeaves
                    .CountAsync(l => l.EmployeeId == employee.Id && l.Status == "Pending");

                ViewBag.MyApprovedLeaveCount = approvedLeaves.Count;

                ViewBag.EmployeeName = employee.FullName;
            }


            return View();
        }


        // ================= ROLES =================
        [AuthorizeAdmin]
        public async Task<IActionResult> Roles()
        {
            return View(await _db.Roles.Where(r => r.IsActive).ToListAsync());
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRole(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Role name is required.";
                return RedirectToAction("Roles");
            }

            if (await _db.Roles.AnyAsync(r => r.IsActive && r.Name == name.Trim()))
            {
                TempData["Error"] = "Role already exists.";
                return RedirectToAction("Roles");
            }

            _db.Roles.Add(new Role { Name = name.Trim(), IsActive = true });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Role created successfully.";
            return RedirectToAction("Roles");
        }

        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> EditRole(int id)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (role == null)
            {
                TempData["Error"] = "Role not found.";
                return RedirectToAction("Roles");
            }

            return View(role);
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(Role model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var role = await _db.Roles.FindAsync(model.Id);

            if (role == null)
                return NotFound();

            role.Name = model.Name;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Role updated successfully.";
            return RedirectToAction("Roles");
        }



        // ================= EMPLOYEES =================
        [AuthorizeAdmin]
        public async Task<IActionResult> Employees()
        {
            ViewBag.Roles = await _db.Roles.Where(r => r.IsActive).ToListAsync();
            ViewBag.ReportingManagers = await _db.Users.Where(u => u.IsActive).ToListAsync();

            var employees = await _db.Employees
                .Include(e => e.User)
                .ThenInclude(u => u.Role)
                .Include(e => e.AddedByUser)
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.AddedDate)
                .ToListAsync();

            return View(employees);
        }

        // ================= CREATE EMPLOYEE =================
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
       
        public async Task<IActionResult> CreateEmployee(CreateEmployeeVM model)
        {
            // -----------------------------------------
            // 1. Validate Model
            // -----------------------------------------
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid data submitted.";
                return RedirectToAction("Employees");
            }

            // -----------------------------------------
            // 2. Get logged-in Admin
            // -----------------------------------------
            var currentUserId = HttpContext.Session.GetInt32("UserId");

            if (currentUserId == null)
            {
                TempData["Error"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------
            // 3. Check duplicate email
            // -----------------------------------------
            var email = model.Email?.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Email is required.";
                return RedirectToAction("Employees");
            }

            if (await _db.Users.AnyAsync(u =>
                u.Email == email &&
                u.IsActive))
            {
                TempData["Error"] = "Email already exists.";
                return RedirectToAction("Employees");
            }

            // -----------------------------------------
            // 4. Create User
            // -----------------------------------------
            var user = new User
            {
                Name = $"{model.FirstName} {model.LastName}".Trim(),
                Email = email,
                RoleId = model.RoleId,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            _db.Users.Add(user);

            await _db.SaveChangesAsync();

            // -----------------------------------------
            // 5. Create Employee
            // -----------------------------------------
            var employee = new Employee
            {
                FirstName = model.FirstName?.Trim(),
                LastName = model.LastName?.Trim(),

                // New employee's login/user
                UserId = user.Id,

                // Admin/HR who created employee
                AddedBy = currentUserId.Value,

                ReportingToUserId = model.ReportingToUserId,

                DateOfBirth = model.DateOfBirth,
                DateOfJoining = model.DateOfJoining,
                Gender = model.Gender,

                // IMPORTANT
                Salary = model.Salary,

                MobileNumber = string.IsNullOrWhiteSpace(model.MobileNumber)
                    ? null
                    : model.MobileNumber.Trim(),

                BloodGroup = string.IsNullOrWhiteSpace(model.BloodGroup)
                    ? null
                    : model.BloodGroup.Trim(),

                IsActive = true,
                IsDeleted = false,
                AddedDate = DateTime.Now
            };

            _db.Employees.Add(employee);

            await _db.SaveChangesAsync();

            // -----------------------------------------
            // 6. Save Educations
            // -----------------------------------------
            if (model.Educations != null && model.Educations.Any())
            {
                foreach (var edu in model.Educations)
                {
                    _db.EmployeeEducations.Add(new EmployeeEducation
                    {
                        EmployeeId = employee.Id,

                        Level = edu.Level,

                        LevelOther = edu.Level == "Other"
                            ? edu.LevelOther
                            : null,

                        Stream = edu.Stream,
                        Institute = edu.Institute,
                        PassingYear = edu.PassingYear,
                        PercentageOrCGPA = edu.PercentageOrCGPA
                    });
                }

                await _db.SaveChangesAsync();
            }

            // -----------------------------------------
            // 7. Save Documents
            // -----------------------------------------
            var files = Request.Form.Files;

            if (files.Count > 0)
            {
                int docIdx = 0;

                foreach (var file in files)
                {
                    if (file == null || file.Length == 0)
                    {
                        docIdx++;
                        continue;
                    }

                    try
                    {
                        var docType =
                            Request.Form[$"Documents[{docIdx}].DocumentType"]
                            .ToString();

                        var docTypeOther =
                            Request.Form[$"Documents[{docIdx}].DocumentTypeOther"]
                            .ToString();

                        if (!string.IsNullOrWhiteSpace(docType))
                        {
                            var blobName =
                                await _blobStorageService.UploadAsync(
                                    file,
                                    employee.Id);

                            _db.EmployeeDocuments.Add(
                                new EmployeeDocument
                                {
                                    EmployeeId = employee.Id,

                                    DocumentType = docType,

                                    DocumentTypeOther =
                                        docType == "Other"
                                            ? docTypeOther
                                            : null,

                                    DocumentName =
                                        Path.GetFileName(file.FileName),

                                    FilePath = blobName,

                                    UploadedDate = DateTime.UtcNow
                                });
                        }

                        docIdx++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Error uploading document: {ex.Message}");

                        docIdx++;
                    }
                }

                if (_db.ChangeTracker.HasChanges())
                {
                    await _db.SaveChangesAsync();
                }
            }

            // -----------------------------------------
            // 8. Success
            // -----------------------------------------
            TempData["Success"] =
                "Employee created successfully.";

            return RedirectToAction("Employees");
        }

        // ================= DELETE EMPLOYEE =================
        [AuthorizeAdminOrHR]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _db.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                TempData["Error"] = "Employee not found.";
                return RedirectToAction("Employees");
            }

            employee.IsActive = false;
            employee.IsDeleted = true;

            if (employee.User != null)
                employee.User.IsActive = false;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Employee deleted successfully.";
            return RedirectToAction("Employees");
        }

        // ================= HOLIDAYS =================
        [SessionAuthorize(SessionAuthMode.AnyLoggedInUser)]
        public IActionResult Holidays(int? year)
        {
            var availableYears = _db.SquadHolidays
                .Where(h => h.IsActive)
                .Select(h => h.HolidayDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            int selectedYear = year ?? (availableYears.Any() ? availableYears.First() : DateTime.Now.Year);

            ViewBag.SelectedYear = selectedYear;
            ViewBag.AvailableYears = availableYears;

            var holidays = _db.SquadHolidays
                .Where(h => h.IsActive && h.HolidayDate.Year == selectedYear)
                .OrderBy(h => h.HolidayDate)
                .ToList();

            return View(holidays);
        }

        // ================= LEAVE APPROVALS =================
        [AuthorizeAdmin]
        public async Task<IActionResult> LeaveApprovals()
        {
            var leaves = await _db.EmployeeLeaves
                .Include(l => l.Employee)
                .Where(l => l.Status == "Pending")
                .OrderByDescending(l => l.AppliedOn)
                .ToListAsync();

            return View(leaves);
        }

        [AuthorizeAdmin]
        [HttpPost]
        public async Task<IActionResult> ApproveLeave(int id)
        {
            var leave = await _db.EmployeeLeaves.FindAsync(id);
            if (leave == null)
                return RedirectToAction("LeaveApprovals");

            leave.Status = "Approved";
            leave.ApprovedByUserId = HttpContext.Session.GetInt32("UserId");

            await _db.SaveChangesAsync();

            // 🔥 Send email to employee
            var employee = await _db.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == leave.EmployeeId);

            if (employee?.User != null)
            {
                var body = _emailService.GetLeaveApprovedTemplate(
               employee.User.Email, // or employee.FirstName
               leave.FromDate,
               leave.ToDate,
               leave.LeaveType,
               leave.Reason
 );

                _emailService.SendEmail(
                    employee.User.Email,
                    "Leave Approved",
                    body
                );
            }

            return RedirectToAction("LeaveManagement");
        }

        [AuthorizeAdmin]
        [HttpPost]
        public async Task<IActionResult> RejectLeave(int id)
        {
            var leave = await _db.EmployeeLeaves.FindAsync(id);
            if (leave == null) return RedirectToAction("LeaveApprovals");

            leave.Status = "Rejected";
            leave.ApprovedByUserId = HttpContext.Session.GetInt32("UserId");
            await _db.SaveChangesAsync();
            var employee = await _db.Employees
    .Include(e => e.User)
    .FirstOrDefaultAsync(e => e.Id == leave.EmployeeId);


            if (employee?.User != null)
            {
                var body = _emailService.GetLeaveRejectedTemplate(
                employee.User.Email,
              leave.FromDate,
              leave.ToDate,
              leave.LeaveType,
              leave.Reason
);

                _emailService.SendEmail(
                    employee.User.Email,
                    "Leave Rejected",
                    body
                );
            }

            return RedirectToAction("LeaveManagement");
        }

        // ================= LEAVE MANAGEMENT =================


        // ================= APPLY LEAVE =================

        [SessionAuthorize(SessionAuthMode.AnyLoggedInUser)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyLeave(EmployeeLeave model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill all required fields.";
                return RedirectToAction("ApplyLeave");
            }

            if (model.ToDate < model.FromDate)
            {
                TempData["Error"] = "To Date cannot be earlier than From Date.";
                return RedirectToAction("ApplyLeave");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null)
            {
                TempData["Error"] = "Employee record not found.";
                return RedirectToAction("Dashboard");
            }

            // 🔥 OVERLAP CHECK (FIXED WITH .Date)
            bool overlappingLeave = await HasOverlappingLeaveAsync(
            employee.Id,
            model.FromDate,
            model.ToDate
            );

            if (overlappingLeave)
            {
                TempData["Error"] = "You already have leave applied for these dates.";
                return RedirectToAction("ApplyLeave");
            }

            model.EmployeeId = employee.Id;
            model.Status = "Pending";
            model.AppliedOn = DateTime.Now;

            _db.EmployeeLeaves.Add(model);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Leave applied successfully.";
            return RedirectToAction("ApplyLeave");
        }

        public async Task<IActionResult> LeaveManagement(int? employeeId, string? status, DateTime? fromDate, DateTime? toDate)
        {
            var leavesQuery = _db.EmployeeLeaves
                .Include(l => l.Employee)
                .AsQueryable();

            var employees = await _db.Employees
                .Where(e => e.IsActive)
                .ToListAsync();

            ViewBag.EmployeeList = employees;
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedStatus = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            if (employeeId.HasValue)
            {
                leavesQuery = leavesQuery.Where(l => l.EmployeeId == employeeId);
            }

            if (fromDate.HasValue)
            {
                leavesQuery = leavesQuery.Where(l => l.ToDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                leavesQuery = leavesQuery.Where(l => l.FromDate.Date <= toDate.Value.Date);
            }

            var baseLeaves = await leavesQuery
                .OrderByDescending(l => l.AppliedOn)
                .ToListAsync();

            ViewBag.TotalLeaves = baseLeaves.Count;
            ViewBag.ApprovedCount = baseLeaves.Count(l => l.Status == "Approved");
            ViewBag.PendingCount = baseLeaves.Count(l => l.Status == "Pending");
            ViewBag.RejectedCount = baseLeaves.Count(l => l.Status == "Rejected");

            var leaves = string.IsNullOrWhiteSpace(status)
                ? baseLeaves
                : baseLeaves.Where(l => l.Status == status).ToList();

            // 🔥 BLOCK ALREADY APPLIED LEAVE DATES FOR CURRENT USER

            var employee = await GetCurrentEmployeeAsync();

            if (employee != null)
            {
                var existingLeaves = await _db.EmployeeLeaves
                    .Where(l => l.EmployeeId == employee.Id &&
                                l.Status != "Rejected" &&
                                l.Status != "Cancelled")
                    .ToListAsync();

                ViewBag.BlockedLeaveDates = existingLeaves
                    .SelectMany(l => ExpandDates(l.FromDate, l.ToDate))
                    .Distinct()
                    .ToList();
            }


            return View(leaves);
        }

        [AuthorizeAdmin]
        public async Task<IActionResult> MyLeaves()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null)
                return Content("Admin employee record not found.");

            var leaves = await _db.EmployeeLeaves
                .Where(l => l.EmployeeId == employee.Id)
                .OrderByDescending(l => l.AppliedOn)
                .ToListAsync();

            return View(leaves);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyAdminLeave(EmployeeLeave model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            if (model.ToDate < model.FromDate)
            {
                TempData["Error"] = "To Date cannot be earlier than From Date.";
                return RedirectToAction("LeaveManagement");
            }

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null)
                return Content("Employee record not found.");

            bool overlappingLeave = await _db.EmployeeLeaves
                .AnyAsync(l =>
                    l.EmployeeId == employee.Id &&
                    l.Status != "Rejected" &&
                    l.Status != "Cancelled" &&
                    model.FromDate.Date <= l.ToDate.Date &&
                    model.ToDate.Date >= l.FromDate.Date
                );

            if (overlappingLeave)
            {
                TempData["Error"] = "You already have leave applied for these dates.";
                return RedirectToAction("LeaveManagement");
            }

            model.EmployeeId = employee.Id;
            model.Status = "Approved";  // Admin auto approve
            model.AppliedOn = DateTime.Now;

            _db.EmployeeLeaves.Add(model);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Leave applied successfully.";
            return RedirectToAction("LeaveManagement");
        }






        // ================= BULK UPLOAD HOLIDAYS =================
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUploadHolidays(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file.";
                return RedirectToAction("BulkUploadHolidays");
            }

            var holidaysAdded = 0;

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);

                using (var package = new OfficeOpenXml.ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                    {
                        TempData["Error"] = "Invalid Excel file.";
                        return RedirectToAction("BulkUploadHolidays");
                    }

                    int rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++) // Skip header
                    {
                        var name = worksheet.Cells[row, 1].Text;
                        var dateText = worksheet.Cells[row, 2].Text;
                        var type = worksheet.Cells[row, 3].Text;

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dateText))
                            continue;

                        if (!DateTime.TryParse(dateText, out DateTime holidayDate))
                            continue;

                        bool exists = await _db.SquadHolidays
                            .AnyAsync(h => h.HolidayDate.Date == holidayDate.Date && h.IsActive);

                        if (!exists)
                        {
                            _db.SquadHolidays.Add(new SquadHoliday
                            {
                                Name = name,
                                HolidayDate = holidayDate,
                                Type = string.IsNullOrEmpty(type) ? "General" : type,
                                IsActive = true,
                                CreatedOn = DateTime.Now
                            });

                            holidaysAdded++;
                        }
                    }

                    await _db.SaveChangesAsync();
                }
            }

            TempData["Success"] = $"{holidaysAdded} holidays uploaded successfully.";
            return RedirectToAction("Holidays");
        }


        // ================= DELETE HOLIDAY =================
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHoliday(int id)
        {
            var holiday = await _db.SquadHolidays
                .FirstOrDefaultAsync(h => h.Id == id);

            if (holiday == null)
            {
                TempData["Error"] = "Holiday not found.";
                return RedirectToAction("Holidays");
            }

            // Soft delete (recommended)
            holiday.IsActive = false;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Holiday deleted successfully.";
            return RedirectToAction("Holidays");
        }

        // ================= EDIT HOLIDAY =================
        [AuthorizeAdmin]
        public async Task<IActionResult> EditHoliday(int id)
        {
            var holiday = await _db.SquadHolidays
                .FirstOrDefaultAsync(h => h.Id == id && h.IsActive);

            if (holiday == null)
            {
                TempData["Error"] = "Holiday not found.";
                return RedirectToAction("Holidays");
            }

            return View(holiday);
        }
        // ================= EDIT HOLIDAY =================


        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHoliday(SquadHoliday model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var holiday = await _db.SquadHolidays.FindAsync(model.Id);

            if (holiday == null)
                return NotFound();

            holiday.Name = model.Name;
            holiday.HolidayDate = model.HolidayDate;
            holiday.Type = model.Type;
            holiday.IsHalfDay = model.IsHalfDay;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Holiday updated successfully.";
            return RedirectToAction("Holidays");
        }
        [AuthorizeAdmin]
        [HttpPost]
        public async Task<IActionResult> UpdateLeaveStatus(int id, string status)
        {
            var leave = await _db.EmployeeLeaves.FindAsync(id);

            if (leave == null)
                return NotFound();

            leave.Status = status;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Leave {status} successfully.";
            return RedirectToAction("LeaveManagement");
        }

        [AuthorizeAdmin]
        public async Task<IActionResult> MonthlySummary(int? month, int? year)
        {
            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            var summary = await BuildMonthlySummaryAsync(selectedMonth, selectedYear);

            return View(summary);
        }

        private async Task<List<MonthlySummaryRowVM>> BuildMonthlySummaryAsync(int month, int year)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var employees = await _db.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .ToListAsync();

            var leaves = await _db.EmployeeLeaves
                .Where(l => l.Status == "Approved" &&
                            l.FromDate.Date <= monthEnd &&
                            l.ToDate.Date >= monthStart)
                .ToListAsync();

            var result = new List<MonthlySummaryRowVM>();

            foreach (var emp in employees)
            {
                var empLeaves = leaves.Where(l => l.EmployeeId == emp.Id);

                double totalLeaveDays = 0;
                foreach (var leave in empLeaves)
                {
                    var overlapStart = leave.FromDate.Date < monthStart ? monthStart : leave.FromDate.Date;
                    var overlapEnd = leave.ToDate.Date > monthEnd ? monthEnd : leave.ToDate.Date;

                    if (overlapEnd >= overlapStart)
                    {
                        totalLeaveDays += (overlapEnd - overlapStart).Days + 1;
                    }
                }

                var paidDays = daysInMonth - totalLeaveDays;
                if (paidDays < 0) paidDays = 0;

                result.Add(new MonthlySummaryRowVM
                {
                    EmployeeId = emp.Id,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    TotalDaysInMonth = daysInMonth,
                    TotalLeavesTaken = totalLeaveDays,
                    TotalPaidDays = paidDays
                });
            }

            return result;
        }

        [AuthorizeAdmin]
        public async Task<IActionResult> DownloadMonthlySummary(int? month, int? year)
        {
            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            var summary = await BuildMonthlySummaryAsync(selectedMonth, selectedYear);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Employee,TotalDaysInMonth,TotalLeavesTaken,TotalPaidDays");

            static string Csv(string? value)
            {
                if (string.IsNullOrEmpty(value)) return string.Empty;
                if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                {
                    return "\"" + value.Replace("\"", "\"\"") + "\"";
                }
                return value;
            }

            foreach (var row in summary)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(row.EmployeeName),
                    row.TotalDaysInMonth.ToString(),
                    row.TotalLeavesTaken.ToString(),
                    row.TotalPaidDays.ToString()
                }));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            var monthName = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM_yyyy");
            var fileName = $"MonthlySummary_{monthName}.csv";
            return File(bytes, "text/csv", fileName);
        }

        [AuthorizeAdmin]
        public async Task<IActionResult> DownloadLeaveReport(int? employeeId, string? status, DateTime? fromDate, DateTime? toDate)
        {
            var leavesQuery = _db.EmployeeLeaves
                .Include(l => l.Employee)
                .AsQueryable();

            if (employeeId.HasValue)
                leavesQuery = leavesQuery.Where(l => l.EmployeeId == employeeId);

            if (!string.IsNullOrWhiteSpace(status))
                leavesQuery = leavesQuery.Where(l => l.Status == status);

            if (fromDate.HasValue)
                leavesQuery = leavesQuery.Where(l => l.ToDate.Date >= fromDate.Value.Date);

            if (toDate.HasValue)
                leavesQuery = leavesQuery.Where(l => l.FromDate.Date <= toDate.Value.Date);

            var leaves = await leavesQuery
                .OrderByDescending(l => l.AppliedOn)
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Employee,LeaveType,FromDate,ToDate,Status,Reason,AppliedOn");

            static string Csv(string? value)
            {
                if (string.IsNullOrEmpty(value)) return string.Empty;
                if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                {
                    return "\"" + value.Replace("\"", "\"\"") + "\"";
                }
                return value;
            }

            foreach (var leave in leaves)
            {
                var employeeName = $"{leave.Employee?.FirstName} {leave.Employee?.LastName}".Trim();
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(employeeName),
                    Csv(leave.LeaveType),
                    leave.FromDate.ToString("dd MMM yyyy"),
                    leave.ToDate.ToString("dd MMM yyyy"),
                    Csv(leave.Status),
                    Csv(leave.Reason),
                    leave.AppliedOn.ToString("dd MMM yyyy HH:mm")
                }));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            var fileName = $"LeaveReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        public IActionResult DownloadHolidayTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(),
                "wwwroot/templates/HolidayTemplate.xlsx");

            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return PhysicalFile(filePath, contentType, "HolidayTemplate.xlsx");
        }

        // ================= CREATE HOLIDAY =================


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateHoliday(SquadHoliday model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill all required fields.";
                return RedirectToAction("Holidays");
            }

            bool exists = await _db.SquadHolidays
                .AnyAsync(h => h.IsActive &&
                               h.HolidayDate.Date == model.HolidayDate.Date);

            if (exists)
            {
                TempData["Error"] = "Holiday already exists on this date.";
                return RedirectToAction("Holidays");
            }

            model.CreatedOn = DateTime.Now;
            model.IsActive = true;

            // 🔥 IMPORTANT FIX
            model.Type = string.IsNullOrEmpty(model.Type) ? "General" : model.Type;

            _db.SquadHolidays.Add(model);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Holiday added successfully.";
            return RedirectToAction("Holidays");
        }

        
        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> GetEmployeeEducations(int employeeId)
        {
            var data = await _db.EmployeeEducations
                .Where(e => e.EmployeeId == employeeId)
                .Select(e => new
                {
                    id = e.Id,
                    level = e.Level,
                    levelOther = e.LevelOther,
                    stream = e.Stream,
                    institute = e.Institute,
                    passingYear = e.PassingYear,
                    percentageOrCGPA = e.PercentageOrCGPA
                })
                .ToListAsync();

            return Json(data);
        }

        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> GetEducation(int id)
        {
            var edu = await _db.EmployeeEducations.FindAsync(id);

            if (edu == null)
                return NotFound();

            return Json(new
            {
                id = edu.Id,
                level = edu.Level,
                levelOther = edu.LevelOther,
                stream = edu.Stream,
                institute = edu.Institute,
                passingYear = edu.PassingYear,
                percentageOrCGPA = edu.PercentageOrCGPA
            });
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEducation([FromBody] EmployeeEducation model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data" });

            if (model.Level != "Other")
            {
                model.LevelOther = null;
            }

            _db.EmployeeEducations.Add(model);
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEducation([FromBody] EmployeeEducation model)
        {
            var edu = await _db.EmployeeEducations.FindAsync(model.Id);

            if (edu == null)
                return NotFound();

            edu.Level = model.Level;
            edu.LevelOther = model.Level == "Other" ? model.LevelOther : null;
            edu.Stream = model.Stream;
            edu.Institute = model.Institute;
            edu.PassingYear = model.PassingYear;
            edu.PercentageOrCGPA = model.PercentageOrCGPA;

            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [AuthorizeAdmin]
        [HttpDelete]
        public async Task<IActionResult> DeleteEducation(int id)
        {
            var edu = await _db.EmployeeEducations.FindAsync(id);

            if (edu == null)
                return NotFound();

            _db.EmployeeEducations.Remove(edu);
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEmployee(
    int id,
    string firstName,
    string lastName,
    int roleId,
    int? reportingToUserId,
    DateTime? dateOfBirth,
    DateTime? dateOfJoining,
    string gender,
    string? mobileNumber,
    string? bloodGroup)
        {
            var employee = await _db.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                TempData["Error"] = "Employee not found.";
                return RedirectToAction("Employees");
            }

            // Update employee table
            employee.FirstName = firstName;
            employee.LastName = lastName;
            employee.ReportingToUserId = reportingToUserId;
            employee.DateOfBirth = dateOfBirth;
            employee.DateOfJoining = dateOfJoining;
            employee.Gender = gender;
            employee.MobileNumber = string.IsNullOrWhiteSpace(mobileNumber) ? null : mobileNumber.Trim();
            employee.BloodGroup = string.IsNullOrWhiteSpace(bloodGroup) ? null : bloodGroup.Trim();

            // Update User table (role)
            if (employee.User != null)
            {
                employee.User.Name = firstName + " " + lastName;
                employee.User.RoleId = roleId;
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Employee updated successfully.";
            return RedirectToAction("Employees");
        }

        private readonly EmailService _emailService;
        private readonly AzureBlobStorageService _blobStorageService;

        public AdminController(AppDbContext db,
                               PasswordService passwordService,
                               EmailService emailService,
                               AzureBlobStorageService blobStorageService)
        {
            _db = db;
            _passwordService = passwordService;
            _emailService = emailService;
            _blobStorageService = blobStorageService;
        }

        // ================= EMPLOYEE DOCUMENTS (ADMIN ONLY) =================

        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> EmployeeDocuments(int employeeId)
        {
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
                return NotFound();

            var documents = await _db.EmployeeDocuments
                .Where(d => d.EmployeeId == employeeId)
                .OrderByDescending(d => d.UploadedDate)
                .ToListAsync();

            ViewBag.Employee = employee;
            ViewBag.DocumentTypes = DocumentTypes.All;

            return PartialView("_EmployeeDocumentsPartial", documents);
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadEmployeeDocument(int employeeId, string documentType, string? documentTypeOther, IFormFile file)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                if (isAjax) return Json(new { success = false, message = "Employee not found." });
                TempData["Error"] = "Employee not found.";
                return RedirectToAction("Employees");
            }

            if (file == null || file.Length == 0)
            {
                if (isAjax) return Json(new { success = false, message = "Please select a file to upload." });
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction("Employees");
            }

            if (string.IsNullOrWhiteSpace(documentType))
            {
                if (isAjax) return Json(new { success = false, message = "Please select a document type." });
                TempData["Error"] = "Please select a document type.";
                return RedirectToAction("Employees");
            }

            if (documentType == "Other" && string.IsNullOrWhiteSpace(documentTypeOther))
            {
                if (isAjax) return Json(new { success = false, message = "Please specify the document type." });
                TempData["Error"] = "Please specify the document type.";
                return RedirectToAction("Employees");
            }

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                if (isAjax) return Json(new { success = false, message = "Unsupported file type. Allowed: PDF, JPG, PNG, DOC, DOCX." });
                TempData["Error"] = "Unsupported file type. Allowed: PDF, JPG, PNG, DOC, DOCX.";
                return RedirectToAction("Employees");
            }

            const long maxFileSize = 10 * 1024 * 1024; // 10 MB
            if (file.Length > maxFileSize)
            {
                if (isAjax) return Json(new { success = false, message = "File size must not exceed 10 MB." });
                TempData["Error"] = "File size must not exceed 10 MB.";
                return RedirectToAction("Employees");
            }

            var blobName = await _blobStorageService.UploadAsync(file, employeeId);

            var document = new EmployeeDocument
            {
                EmployeeId = employeeId,
                DocumentType = documentType,
                DocumentTypeOther = documentType == "Other" ? documentTypeOther?.Trim() : null,
                DocumentName = Path.GetFileName(file.FileName),
                FilePath = blobName,
                UploadedDate = DateTime.UtcNow
            };

            _db.EmployeeDocuments.Add(document);
            await _db.SaveChangesAsync();

            if (isAjax) return Json(new { success = true, message = "Document uploaded successfully." });

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction("Employees");
        }

        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> DownloadEmployeeDocument(int id)
        {
            var document = await _db.EmployeeDocuments.FindAsync(id);

            if (document == null)
                return NotFound();

            var (content, contentType) = await _blobStorageService.DownloadAsync(document.FilePath);

            return File(content, contentType, document.DocumentName);
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployeeDocument(int id, int employeeId)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            var document = await _db.EmployeeDocuments.FindAsync(id);

            if (document == null)
            {
                if (isAjax) return Json(new { success = false, message = "Document not found." });
                TempData["Error"] = "Document not found.";
                return RedirectToAction("Employees");
            }

            await _blobStorageService.DeleteAsync(document.FilePath);

            _db.EmployeeDocuments.Remove(document);
            await _db.SaveChangesAsync();

            if (isAjax) return Json(new { success = true, message = "Document deleted successfully." });

            TempData["Success"] = "Document deleted successfully.";
            return RedirectToAction("Employees");
        }

        // ================= EMPLOYEE PROFILE PICTURE (ADMIN ONLY) =================

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture(int employeeId, IFormFile file)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                if (isAjax) return Json(new { success = false, message = "Employee not found." });
                TempData["Error"] = "Employee not found.";
                return RedirectToAction("Employees");
            }

            if (file == null || file.Length == 0)
            {
                if (isAjax) return Json(new { success = false, message = "Please select an image to upload." });
                TempData["Error"] = "Please select an image to upload.";
                return RedirectToAction("Employees");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                if (isAjax) return Json(new { success = false, message = "Unsupported file type. Allowed: JPG, PNG, WEBP." });
                TempData["Error"] = "Unsupported file type. Allowed: JPG, PNG, WEBP.";
                return RedirectToAction("Employees");
            }

            const long maxFileSize = 5 * 1024 * 1024; // 5 MB
            if (file.Length > maxFileSize)
            {
                if (isAjax) return Json(new { success = false, message = "Image size must not exceed 5 MB." });
                TempData["Error"] = "Image size must not exceed 5 MB.";
                return RedirectToAction("Employees");
            }

            var oldBlobName = employee.ProfilePicturePath;

            var blobName = await _blobStorageService.UploadAsync(file, employeeId);
            employee.ProfilePicturePath = blobName;

            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(oldBlobName))
            {
                await _blobStorageService.DeleteAsync(oldBlobName);
            }

            if (isAjax) return Json(new { success = true, message = "Profile picture updated successfully.", url = Url.Action("GetProfilePicture", "Admin", new { employeeId }) });

            TempData["Success"] = "Profile picture updated successfully.";
            return RedirectToAction("Employees");
        }

        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> GetProfilePicture(int employeeId)
        {
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null || string.IsNullOrWhiteSpace(employee.ProfilePicturePath))
                return NotFound();

            var (content, contentType) = await _blobStorageService.DownloadAsync(employee.ProfilePicturePath);

            Response.Headers["Cache-Control"] = "no-cache";
            return File(content, contentType);
        }

        [HttpGet]
        public async Task<IActionResult> ApproveFromEmail(int id)
        {
            var leave = await _db.EmployeeLeaves.FindAsync(id);

            if (leave == null)
                return Content("Leave not found.");

            leave.Status = "Approved";
            await _db.SaveChangesAsync();

            return Content("Leave Approved Successfully.");
        }

        [HttpGet]
        public async Task<IActionResult> RejectFromEmail(int id)
        {
            var leave = await _db.EmployeeLeaves.FindAsync(id);

            if (leave == null)
                return Content("Leave not found.");

            leave.Status = "Rejected";
            await _db.SaveChangesAsync();

            return Content("Leave Rejected Successfully.");
        }



    }
}


