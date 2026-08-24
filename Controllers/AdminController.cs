using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Filters;
using SquadInternal.Models;
using SquadInternal.Models.Enum;
using SquadInternal.Models.ViewModels;
using SquadInternal.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


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

            // ==========================================
            // DASHBOARD COUNTS
            // ==========================================

            ViewBag.EmployeeCount =
                await _db.Employees.CountAsync(e => e.IsActive);

            ViewBag.RoleCount =
                await _db.Roles.CountAsync(r => r.IsActive);


            // ==========================================
            // HOLIDAYS
            // ==========================================

            var holidayDates = await _db.SquadHolidays
                .Where(h => h.IsActive)
                .Select(h => h.HolidayDate.Date)
                .ToListAsync();

            ViewBag.Holidays = holidayDates;

            ViewBag.UpcomingHolidays = await _db.SquadHolidays
                .Where(h =>
                    h.IsActive &&
                    h.HolidayDate.Date >= DateTime.Today)
                .OrderBy(h => h.HolidayDate)
                .Take(5)
                .ToListAsync();

            ViewBag.Today = DateTime.Today;


            // ==========================================
            // CURRENT EMPLOYEE
            // ==========================================

            var employee = await GetCurrentEmployeeAsync();


            // ==========================================
            // ADMIN
            // Show ALL approved employee leaves
            // ==========================================

            if (isAdmin)
            {
                ViewBag.PendingLeaveCount =
                    await _db.EmployeeLeaves
                        .CountAsync(l => l.Status == "Pending");

                var allApprovedLeaves = await _db.EmployeeLeaves
                    .Where(l => l.Status == "Approved")
                    .ToListAsync();

                ViewBag.ApprovedLeaveDates = allApprovedLeaves
                    .SelectMany(l => ExpandDates(l.FromDate, l.ToDate))
                    .Distinct()
                    .ToList();
            }


            // ==========================================
            // NORMAL EMPLOYEE
            // Show only their own approved leaves
            // ==========================================

            else if (employee != null)
            {
                var approvedLeaves = await _db.EmployeeLeaves
                    .Where(l =>
                        l.EmployeeId == employee.Id &&
                        l.Status == "Approved")
                    .ToListAsync();

                ViewBag.ApprovedLeaveDates = approvedLeaves
                    .SelectMany(l => ExpandDates(l.FromDate, l.ToDate))
                    .Distinct()
                    .ToList();

                ViewBag.MyPendingLeaveCount =
                    await _db.EmployeeLeaves
                        .CountAsync(l =>
                            l.EmployeeId == employee.Id &&
                            l.Status == "Pending");

                ViewBag.MyApprovedLeaveCount =
                    approvedLeaves.Count;

                ViewBag.EmployeeName =
                    employee.FullName;
            }


            // ==========================================
            // SAFETY
            // ==========================================

            if (ViewBag.ApprovedLeaveDates == null)
            {
                ViewBag.ApprovedLeaveDates = new List<DateTime>();
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
            // 3. Validate Email
            // -----------------------------------------
            var email = model.Email?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Email is required.";
                return RedirectToAction("Employees");
            }

            // -----------------------------------------
            // 4. Check Duplicate Email
            // -----------------------------------------
            if (await _db.Users.AnyAsync(u =>
                u.Email != null &&
                u.Email.ToLower() == email))
            {
                TempData["Error"] = "Email already exists.";
                return RedirectToAction("Employees");
            }

            // -----------------------------------------
            // 5. Create User
            // -----------------------------------------
            var temporaryPassword =
                 $"SqL@{Guid.NewGuid():N}".Substring(0, 12);

            var user = new User
            {
                Name = $"{model.FirstName} {model.LastName}".Trim(),
                Email = email,

                RoleId = model.RoleId,

                IsActive = true,

                // Store only the hash.
                // Never send this temporary password by email.
                PasswordHash = _passwordService.HashPassword(
                    temporaryPassword),

                CreatedDate = DateTime.Now
            };

            _db.Users.Add(user);

            await _db.SaveChangesAsync();

            // -----------------------------------------
            // 6. Create Employee
            // -----------------------------------------
            var employee = new Employee
            {
                FirstName = model.FirstName?.Trim(),
                LastName = model.LastName?.Trim(),

                UserId = user.Id,

                // Admin who created employee
                AddedBy = currentUserId.Value,

                ReportingToUserId = model.ReportingToUserId,

                DateOfBirth = model.DateOfBirth,
                DateOfJoining = model.DateOfJoining,
                Gender = model.Gender,

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
            // 7. Save Educations
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
            // 8. Save Documents
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
                            // Upload document
                            var blobName =
                                await _blobStorageService.UploadAsync(
                                    file,
                                    employee.Id);

                            // Save document record
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
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Error uploading document {file.FileName}: {ex}");

                        // Continue processing remaining documents
                    }

                    docIdx++;
                }

                // Save all documents
                if (_db.ChangeTracker.HasChanges())
                {
                    await _db.SaveChangesAsync();
                }
            }

            // ============================================================
            // 9. SEND WELCOME EMAIL
            // ============================================================
            // IMPORTANT:
            // This MUST be outside the document foreach loop.
            // ============================================================

            try
            {
                var portalUrl =
                    "https://squadinternal.azurewebsites.net/Account/ForgotPassword";

                // Get role name
                var roleName = await _db.Roles
                    .Where(r => r.Id == model.RoleId)
                    .Select(r => r.Name)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(roleName))
                {
                    roleName = "Employee";
                }

                var employeeName =
                    $"{employee.FirstName} {employee.LastName}".Trim();

                // Generate welcome email
                var emailBody =
                    _emailService.GetEmployeeWelcomeTemplate(
                        employeeName,
                        user.Email,
                        roleName,
                        portalUrl);

                // Send welcome email
                _emailService.SendEmail(
                    user.Email,
                    "Welcome to Squad Logic | Employee Portal Access",
                    emailBody);

                Console.WriteLine(
                    $"Welcome email sent successfully to {user.Email}");
            }
            catch (Exception ex)
            {
                // IMPORTANT:
                // Employee is already created.
                // Don't delete employee if email fails.

                Console.WriteLine(
                    $"Welcome email failed for {user.Email}: {ex}");

                TempData["Warning"] =
                    "Employee created successfully, but the welcome email could not be sent.";
            }

            // -----------------------------------------
            // 10. Success
            // -----------------------------------------
            if (TempData["Warning"] == null)
            {
                TempData["Success"] =
                    "Employee created successfully and welcome email sent.";
            }
            else
            {
                TempData["Success"] =
                    "Employee created successfully.";
            }

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
    string? gender,
    string? mobileNumber,
    string? bloodGroup,
    decimal? salary)
        {
            var employee = await _db.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                TempData["Error"] = "Employee not found.";
                return RedirectToAction("Employees");
            }

            // Employee table
            employee.FirstName = firstName?.Trim();
            employee.LastName = lastName?.Trim();

            employee.ReportingToUserId = reportingToUserId;

            employee.DateOfBirth = dateOfBirth;
            employee.DateOfJoining = dateOfJoining;
            employee.Gender = string.IsNullOrWhiteSpace(gender)
                ? null
                : gender.Trim();

            employee.MobileNumber = string.IsNullOrWhiteSpace(mobileNumber)
                ? null
                : mobileNumber.Trim();

            employee.BloodGroup = string.IsNullOrWhiteSpace(bloodGroup)
                ? null
                : bloodGroup.Trim();

            employee.Salary = salary;

            // User table
            if (employee.User != null)
            {
                employee.User.Name =
                    $"{employee.FirstName} {employee.LastName}".Trim();

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
        public async Task<IActionResult> Assets()
        {
            var assets = await _db.Assets
                .Include(a => a.AssetCategory)
                .Include(a => a.CurrentEmployee)
                .Where(a => !a.IsDeleted)
                .OrderByDescending(a => a.AddedDate)
                .ToListAsync();

            ViewBag.AssetCategories = await _db.AssetCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // Provide employee list so assign modal dropdown can be populated
            ViewBag.Employees = await _db.Employees
                .Where(e => e.IsActive && !e.IsDeleted)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();

            return View(assets);
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAsset(AssetVM model)
        {
            try
            {
                // =========================================================
                // 1. DEBUG LOGGING
                // =========================================================

                Console.WriteLine("=================================");
                Console.WriteLine("ADD ASSET REQUEST - MODEL");
                Console.WriteLine($"AssetCode       : {model.AssetCode}");
                Console.WriteLine($"AssetName       : {model.AssetName}");
                Console.WriteLine($"AssetCategoryId : {model.AssetCategoryId}");
                Console.WriteLine($"Condition       : {model.Condition}");
                Console.WriteLine("=================================");

                // =========================================================
                // 2. CURRENT USER
                // =========================================================

                var currentUserId = HttpContext.Session.GetInt32("UserId");

                if (currentUserId == null)
                {
                    TempData["Error"] = "Your session has expired. Please login again.";
                    return RedirectToAction("Login", "Account");
                }

                // =========================================================
                // 3. CLEAN STRING VALUES
                // =========================================================

                if (string.IsNullOrWhiteSpace(model.AssetCode) &&
     Request.Form.ContainsKey("AssetCode"))
                {
                    model.AssetCode = Request.Form["AssetCode"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.AssetName) &&
                    Request.Form.ContainsKey("AssetName"))
                {
                    model.AssetName = Request.Form["AssetName"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.Brand) &&
                    Request.Form.ContainsKey("Brand"))
                {
                    model.Brand = Request.Form["Brand"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.Model) &&
                    Request.Form.ContainsKey("Model"))
                {
                    model.Model = Request.Form["Model"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.SerialNumber) &&
                    Request.Form.ContainsKey("SerialNumber"))
                {
                    model.SerialNumber = Request.Form["SerialNumber"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.VendorName) &&
                    Request.Form.ContainsKey("VendorName"))
                {
                    model.VendorName = Request.Form["VendorName"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.InvoiceNumber) &&
                    Request.Form.ContainsKey("InvoiceNumber"))
                {
                    model.InvoiceNumber = Request.Form["InvoiceNumber"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.Configuration) &&
                    Request.Form.ContainsKey("Configuration"))
                {
                    model.Configuration = Request.Form["Configuration"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.Description) &&
                    Request.Form.ContainsKey("Description"))
                {
                    model.Description = Request.Form["Description"].ToString();
                }

                if (string.IsNullOrWhiteSpace(model.Location) &&
                    Request.Form.ContainsKey("Location"))
                {
                    model.Location = Request.Form["Location"].ToString();
                }

                // Category fallback
                if (model.AssetCategoryId <= 0 &&
                    Request.Form.ContainsKey("AssetCategoryId") &&
                    int.TryParse(Request.Form["AssetCategoryId"], out var categoryId))
                {
                    model.AssetCategoryId = categoryId;
                }

                // Condition fallback
                if (Request.Form.ContainsKey("Condition"))
                {
                    model.Condition = Request.Form["Condition"].ToString();
                }

                // Numeric fallback
                if (!model.PurchaseCost.HasValue &&
                    Request.Form.ContainsKey("PurchaseCost") &&
                    decimal.TryParse(Request.Form["PurchaseCost"], out var purchaseCost))
                {
                    model.PurchaseCost = purchaseCost;
                }

                // Date fallbacks
                if (!model.PurchaseDate.HasValue &&
                    Request.Form.ContainsKey("PurchaseDate") &&
                    DateTime.TryParse(Request.Form["PurchaseDate"], out var purchaseDate))
                {
                    model.PurchaseDate = purchaseDate;
                }

                if (!model.WarrantyStartDate.HasValue &&
                    Request.Form.ContainsKey("WarrantyStartDate") &&
                    DateTime.TryParse(Request.Form["WarrantyStartDate"], out var warrantyStart))
                {
                    model.WarrantyStartDate = warrantyStart;
                }

                if (!model.WarrantyEndDate.HasValue &&
                    Request.Form.ContainsKey("WarrantyEndDate") &&
                    DateTime.TryParse(Request.Form["WarrantyEndDate"], out var warrantyEnd))
                {
                    model.WarrantyEndDate = warrantyEnd;
                }


                // =========================================================
                // 4. CLEAN STRING VALUES
                // =========================================================

                model.AssetCode = model.AssetCode?.Trim();
                model.AssetName = model.AssetName?.Trim();
                model.Brand = model.Brand?.Trim();
                model.Model = model.Model?.Trim();
                model.SerialNumber = model.SerialNumber?.Trim();
                model.VendorName = model.VendorName?.Trim();
                model.InvoiceNumber = model.InvoiceNumber?.Trim();
                model.Configuration = model.Configuration?.Trim();
                model.Description = model.Description?.Trim();
                model.Location = model.Location?.Trim();
                model.Condition = model.Condition?.Trim();

                // =========================================================
                // 4. VALIDATE REQUIRED FIELDS
                // =========================================================

                if (string.IsNullOrWhiteSpace(model.AssetCode))
                {
                    TempData["Error"] = "Asset code is required.";
                    return RedirectToAction("Assets");
                }

                if (string.IsNullOrWhiteSpace(model.AssetName))
                {
                    TempData["Error"] = "Asset name is required.";
                    return RedirectToAction("Assets");
                }

                if (model.AssetCategoryId <= 0)
                {
                    TempData["Error"] = "Please select a valid asset category.";
                    return RedirectToAction("Assets");
                }

                // =========================================================
                // 5. CHECK CATEGORY EXISTS
                // =========================================================

                var category = await _db.AssetCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == model.AssetCategoryId && c.IsActive);

                if (category == null)
                {
                    TempData["Error"] = $"Asset category with ID {model.AssetCategoryId} does not exist or is inactive.";
                    return RedirectToAction("Assets");
                }

                // =========================================================
                // 6. CHECK DUPLICATE ASSET CODE
                // =========================================================

                var assetCodeExists = await _db.Assets
                    .AsNoTracking()
                    .AnyAsync(a => a.AssetCode == model.AssetCode && !a.IsDeleted);

                if (assetCodeExists)
                {
                    TempData["Error"] = $"Asset code '{model.AssetCode}' already exists.";
                    return RedirectToAction("Assets");
                }

                // =========================================================
                // 7. CHECK DUPLICATE SERIAL NUMBER
                // =========================================================

                if (!string.IsNullOrWhiteSpace(model.SerialNumber))
                {
                    var serialExists = await _db.Assets
                        .AsNoTracking()
                        .AnyAsync(a => a.SerialNumber == model.SerialNumber && !a.IsDeleted);

                    if (serialExists)
                    {
                        TempData["Error"] = $"Serial number '{model.SerialNumber}' already exists.";
                        return RedirectToAction("Assets");
                    }
                }

                // =========================================================
                // 8. CONVERT CONDITION STRING TO ENUM
                // =========================================================

                AssetCondition conditionEnum;
                if (!Enum.TryParse<AssetCondition>(model.Condition, true, out conditionEnum))
                {
                    conditionEnum = AssetCondition.New;
                    Console.WriteLine($"Failed to parse condition '{model.Condition}', defaulting to New");
                }

                // =========================================================
                // 9. CREATE ASSET
                // =========================================================

                var asset = new Asset
                {
                    AssetCode = model.AssetCode,
                    AssetCategoryId = model.AssetCategoryId,
                    AssetName = model.AssetName,
                    Brand = model.Brand,
                    Model = model.Model,
                    SerialNumber = model.SerialNumber,
                    VendorName = model.VendorName,
                    InvoiceNumber = model.InvoiceNumber,
                    PurchaseDate = model.PurchaseDate,
                    PurchaseCost = model.PurchaseCost,
                    WarrantyStartDate = model.WarrantyStartDate,
                    WarrantyEndDate = model.WarrantyEndDate,
                    Configuration = model.Configuration,
                    Description = model.Description,
                    Location = model.Location,
                    Condition = conditionEnum,
                    Status = AssetStatus.Available,
                    CurrentEmployeeId = null,
                    IsActive = true,
                    IsDeleted = false,
                    AddedBy = currentUserId.Value,
                    AddedDate = DateTime.Now
                };

                // =========================================================
                // 10. INSERT ASSET
                // =========================================================

                _db.Assets.Add(asset);
                await _db.SaveChangesAsync();

                // =========================================================
                // 11. ADD ASSET HISTORY
                // =========================================================

                _db.AssetHistories.Add(new AssetHistory
                {
                    AssetId = asset.Id,
                    Action = "Asset Created",
                    NewValue = asset.AssetName,
                    Remarks = "Asset added to Asset Master.",
                    PerformedBy = currentUserId.Value,
                    PerformedDate = DateTime.Now
                });

                await _db.SaveChangesAsync();

                TempData["Success"] = $"Asset '{asset.AssetName}' added successfully.";
                return RedirectToAction("Assets");
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"DATABASE ERROR: {ex.Message}");
                Console.WriteLine($"INNER: {ex.InnerException?.Message}");
                TempData["Error"] = $"Database error: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction("Assets");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"STACK: {ex.StackTrace}");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Assets");
            }
        }
        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> EditAsset(int id)
        {
            var asset = await _db.Assets
                .Include(a => a.AssetCategory)
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&

                    !a.IsDeleted);

            if (asset == null)
            {
                // Helpful diagnostic: check if asset exists but is deleted
                var existsDeleted = await _db.Assets
                    .AnyAsync(a => a.Id == id && a.IsDeleted);

                if (existsDeleted)
                {
                    Console.WriteLine($"EDIT ASSET GET: Asset id={id} exists but IsDeleted=true");
                    TempData["Error"] = "Asset not found (it may have been deleted).";
                }
                else
                {
                    Console.WriteLine($"EDIT ASSET GET: Asset id={id} not found in database");
                    TempData["Error"] = "Asset not found.";
                }

                return RedirectToAction("Assets");
            }

            // Active categories for dropdown
            ViewBag.AssetCategories = await _db.AssetCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // Map entity to VM
            var vm = new Models.ViewModels.AssetVM
            {
                Id = asset.Id,
                AssetCode = asset.AssetCode,
                AssetCategoryId = asset.AssetCategoryId,
                AssetName = asset.AssetName,
                Brand = asset.Brand,
                Model = asset.Model,
                SerialNumber = asset.SerialNumber,
                VendorName = asset.VendorName,
                InvoiceNumber = asset.InvoiceNumber,
                PurchaseDate = asset.PurchaseDate,
                PurchaseCost = asset.PurchaseCost,
                WarrantyStartDate = asset.WarrantyStartDate,
                WarrantyEndDate = asset.WarrantyEndDate,
                Configuration = asset.Configuration,
                Description = asset.Description,
                Location = asset.Location,
                Condition = asset.Condition.ToString()
            };

            return View(vm);
        }
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAsset(Models.ViewModels.AssetVM model)
        {
            try
            {
                // =========================================
                // =========================================
                // DEBUG: Log the incoming raw form (useful when model binding fails)
                Console.WriteLine("--- EditAsset POST: Form fields ---");
                foreach (var key in Request.Form.Keys)
                {
                    Console.WriteLine($"FORM[{key}] = {Request.Form[key]}");
                }
                Console.WriteLine("--- End Form fields ---");

                // DEBUG: Log the incoming model
                // =========================================

                Console.WriteLine("=================================");
                Console.WriteLine("EDIT ASSET POST RECEIVED");
                Console.WriteLine($"Model Id: {model.Id}");
                Console.WriteLine($"Model AssetCode: {model.AssetCode}");
                Console.WriteLine($"Model AssetName: {model.AssetName}");
                Console.WriteLine($"Model CategoryId: {model.AssetCategoryId}");
                Console.WriteLine("=================================");

                // Fallbacks: sometimes model binding can lose the Id (hidden field missing/name mismatch).
                // Try to recover Id from form values or route data before proceeding.
                if (model.Id <= 0)
                {
                    // Try common form names
                    var formId = Request.Form["Id"].FirstOrDefault() ?? Request.Form["id"].FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(formId) && int.TryParse(formId, out var parsedId))
                    {
                        model.Id = parsedId;
                        Console.WriteLine($"EDIT ASSET POST: recovered model.Id from form = {model.Id}");
                    }
                    else if (RouteData.Values.ContainsKey("id") && int.TryParse(RouteData.Values["id"]?.ToString(), out var routeId))
                    {
                        model.Id = routeId;
                        Console.WriteLine($"EDIT ASSET POST: recovered model.Id from route = {model.Id}");
                    }
                    else
                    {
                        Console.WriteLine("EDIT ASSET POST: model.Id missing and could not be recovered from form/route");
                    }
                }

                // =========================================
                // 1. CURRENT USER
                // =========================================

                var currentUserId = HttpContext.Session.GetInt32("UserId");

                if (currentUserId == null)
                {
                    TempData["Error"] =
                        "Your session has expired. Please login again.";

                    return RedirectToAction("Login", "Account");
                }

                // =========================================
                // 2. VALIDATE ID
                // =========================================

                if (model.Id <= 0)
                {
                    Console.WriteLine("ERROR: Model Id is 0 or less");
                    TempData["Error"] = "Invalid asset ID.";
                    return RedirectToAction("Assets");
                }

                // =========================================
                // 3. FIND EXISTING ASSET
                // =========================================

                var asset = await _db.Assets
                    .FirstOrDefaultAsync(a =>
                        a.Id == model.Id &&
                        !a.IsDeleted);

                if (asset == null)
                {
                    Console.WriteLine($"ERROR: Asset with ID {model.Id} not found");
                    TempData["Error"] = "Asset not found.";
                    return RedirectToAction("Assets");
                }

                Console.WriteLine($"Asset found: {asset.Id} - {asset.AssetCode}");

                // =========================================
                // 4. CLEAN VALUES
                // =========================================
                // If model binder failed for some fields, fall back to raw form values so the view redisplays user input
                if (string.IsNullOrWhiteSpace(model.AssetCode) && Request.Form.ContainsKey("AssetCode"))
                    model.AssetCode = Request.Form["AssetCode"].ToString();

                if (string.IsNullOrWhiteSpace(model.AssetName) && Request.Form.ContainsKey("AssetName"))
                    model.AssetName = Request.Form["AssetName"].ToString();

                if (string.IsNullOrWhiteSpace(model.Brand) && Request.Form.ContainsKey("Brand"))
                    model.Brand = Request.Form["Brand"].ToString();

                if (string.IsNullOrWhiteSpace(model.Model) && Request.Form.ContainsKey("Model"))
                    model.Model = Request.Form["Model"].ToString();

                if (string.IsNullOrWhiteSpace(model.SerialNumber) && Request.Form.ContainsKey("SerialNumber"))
                    model.SerialNumber = Request.Form["SerialNumber"].ToString();

                if (string.IsNullOrWhiteSpace(model.VendorName) && Request.Form.ContainsKey("VendorName"))
                    model.VendorName = Request.Form["VendorName"].ToString();

                if (string.IsNullOrWhiteSpace(model.InvoiceNumber) && Request.Form.ContainsKey("InvoiceNumber"))
                    model.InvoiceNumber = Request.Form["InvoiceNumber"].ToString();

                if (string.IsNullOrWhiteSpace(model.Configuration) && Request.Form.ContainsKey("Configuration"))
                    model.Configuration = Request.Form["Configuration"].ToString();

                if (string.IsNullOrWhiteSpace(model.Description) && Request.Form.ContainsKey("Description"))
                    model.Description = Request.Form["Description"].ToString();

                if (string.IsNullOrWhiteSpace(model.Location) && Request.Form.ContainsKey("Location"))
                    model.Location = Request.Form["Location"].ToString();

                // Parse numeric/date fallbacks
                if (!model.PurchaseCost.HasValue && Request.Form.ContainsKey("PurchaseCost") && decimal.TryParse(Request.Form["PurchaseCost"], out var pc))
                    model.PurchaseCost = pc;

                if (!model.PurchaseDate.HasValue && Request.Form.ContainsKey("PurchaseDate") && DateTime.TryParse(Request.Form["PurchaseDate"], out var pd))
                    model.PurchaseDate = pd;

                if (!model.WarrantyStartDate.HasValue && Request.Form.ContainsKey("WarrantyStartDate") && DateTime.TryParse(Request.Form["WarrantyStartDate"], out var ws))
                    model.WarrantyStartDate = ws;

                if (!model.WarrantyEndDate.HasValue && Request.Form.ContainsKey("WarrantyEndDate") && DateTime.TryParse(Request.Form["WarrantyEndDate"], out var we))
                    model.WarrantyEndDate = we;

                // Category fallback
                if (model.AssetCategoryId <= 0 && Request.Form.ContainsKey("AssetCategoryId") && int.TryParse(Request.Form["AssetCategoryId"], out var catId))
                    model.AssetCategoryId = catId;

                // Condition fallback (enum integer)
                if (Request.Form.ContainsKey("Condition") && Enum.TryParse<AssetCondition>(Request.Form["Condition"], out var cond))
                    model.Condition = cond.ToString();

                // =========================================
                // 5. REQUIRED VALIDATION
                // =========================================

                if (string.IsNullOrWhiteSpace(model.AssetCode))
                {
                    ModelState.AddModelError(nameof(model.AssetCode), "Asset code is required.");

                    ViewBag.AssetCategories = await _db.AssetCategories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToListAsync();

                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.AssetName))
                {
                    ModelState.AddModelError(nameof(model.AssetName), "Asset name is required.");

                    ViewBag.AssetCategories = await _db.AssetCategories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToListAsync();

                    return View(model);
                }

                if (model.AssetCategoryId <= 0)
                {
                    ModelState.AddModelError(nameof(model.AssetCategoryId), "Please select a valid asset category.");

                    ViewBag.AssetCategories = await _db.AssetCategories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToListAsync();

                    return View(model);
                }

                // =========================================
                // 6. CATEGORY VALIDATION
                // =========================================

                var categoryExists = await _db.AssetCategories
                    .AnyAsync(c =>
                        c.Id == model.AssetCategoryId &&
                        c.IsActive);

                if (!categoryExists)
                {
                    ModelState.AddModelError(nameof(model.AssetCategoryId), "Selected asset category is invalid or inactive.");

                    ViewBag.AssetCategories = await _db.AssetCategories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToListAsync();

                    return View(model);
                }

                // =========================================
                // 7. DUPLICATE ASSET CODE
                // =========================================

                bool duplicateCode = await _db.Assets
                    .AnyAsync(a =>
                        a.Id != model.Id &&
                        a.AssetCode == model.AssetCode &&
                        !a.IsDeleted);

                if (duplicateCode)
                {
                    ModelState.AddModelError(nameof(model.AssetCode), $"Asset code '{model.AssetCode}' already exists.");

                    ViewBag.AssetCategories = await _db.AssetCategories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToListAsync();

                    return View(model);
                }

                // =========================================
                // 8. DUPLICATE SERIAL NUMBER
                // =========================================

                if (!string.IsNullOrWhiteSpace(model.SerialNumber))
                {
                    bool duplicateSerial = await _db.Assets
                        .AnyAsync(a =>
                            a.Id != model.Id &&
                            a.SerialNumber == model.SerialNumber &&
                            !a.IsDeleted);

                    if (duplicateSerial)
                    {
                        ModelState.AddModelError(nameof(model.SerialNumber), $"Serial number '{model.SerialNumber}' already exists.");

                        ViewBag.AssetCategories = await _db.AssetCategories
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.Name)
                            .ToListAsync();

                        return View(model);
                    }
                }

                // =========================================
                // 9. CONDITION VALIDATION
                // =========================================

                // Parse condition string to enum
                if (!Enum.TryParse<AssetCondition>(model.Condition, true, out var parsedCondition))
                {
                    ModelState.AddModelError(nameof(model.Condition), "Please select a valid asset condition.");

                    ViewBag.AssetCategories = await _db.AssetCategories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToListAsync();

                    return View(model);
                }

                // =========================================
                // 10. STORE OLD VALUES FOR HISTORY
                // =========================================

                var oldValues = new
                {
                    AssetCode = asset.AssetCode,
                    AssetName = asset.AssetName,
                    CategoryId = asset.AssetCategoryId,
                    Brand = asset.Brand,
                    Model = asset.Model,
                    SerialNumber = asset.SerialNumber,
                    VendorName = asset.VendorName,
                    InvoiceNumber = asset.InvoiceNumber,
                    PurchaseDate = asset.PurchaseDate,
                    PurchaseCost = asset.PurchaseCost,
                    WarrantyStartDate = asset.WarrantyStartDate,
                    WarrantyEndDate = asset.WarrantyEndDate,
                    Configuration = asset.Configuration,
                    Description = asset.Description,
                    Location = asset.Location,
                    Condition = asset.Condition
                };

                // =========================================
                // 11. UPDATE ASSET
                // =========================================

                asset.AssetCode = model.AssetCode;
                asset.AssetName = model.AssetName;
                asset.AssetCategoryId = model.AssetCategoryId;
                asset.Brand = model.Brand;
                asset.Model = model.Model;
                asset.SerialNumber = model.SerialNumber;
                asset.VendorName = model.VendorName;
                asset.InvoiceNumber = model.InvoiceNumber;
                asset.PurchaseDate = model.PurchaseDate;
                asset.PurchaseCost = model.PurchaseCost;
                asset.WarrantyStartDate = model.WarrantyStartDate;
                asset.WarrantyEndDate = model.WarrantyEndDate;
                asset.Configuration = model.Configuration;
                asset.Description = model.Description;
                asset.Location = model.Location;
                asset.Condition = parsedCondition;
                asset.UpdatedDate = DateTime.Now;

                // =========================================
                // 12. BUILD CHANGE HISTORY
                // =========================================

                var changes = new List<string>();

                if (oldValues.AssetCode != asset.AssetCode)
                    changes.Add($"Asset Code: {oldValues.AssetCode} → {asset.AssetCode}");

                if (oldValues.AssetName != asset.AssetName)
                    changes.Add($"Asset Name: {oldValues.AssetName} → {asset.AssetName}");

                if (oldValues.CategoryId != asset.AssetCategoryId)
                    changes.Add($"Category: {oldValues.CategoryId} → {asset.AssetCategoryId}");

                if (oldValues.Brand != asset.Brand)
                    changes.Add($"Brand: {oldValues.Brand ?? "N/A"} → {asset.Brand ?? "N/A"}");

                if (oldValues.Model != asset.Model)
                    changes.Add($"Model: {oldValues.Model ?? "N/A"} → {asset.Model ?? "N/A"}");

                if (oldValues.SerialNumber != asset.SerialNumber)
                    changes.Add($"Serial Number: {oldValues.SerialNumber ?? "N/A"} → {asset.SerialNumber ?? "N/A"}");

                if (oldValues.VendorName != asset.VendorName)
                    changes.Add($"Vendor: {oldValues.VendorName ?? "N/A"} → {asset.VendorName ?? "N/A"}");

                if (oldValues.InvoiceNumber != asset.InvoiceNumber)
                    changes.Add($"Invoice: {oldValues.InvoiceNumber ?? "N/A"} → {asset.InvoiceNumber ?? "N/A"}");

                if (oldValues.PurchaseCost != asset.PurchaseCost)
                    changes.Add($"Cost: {oldValues.PurchaseCost?.ToString("N2") ?? "N/A"} → {asset.PurchaseCost?.ToString("N2") ?? "N/A"}");

                if (oldValues.Condition != asset.Condition)
                    changes.Add($"Condition: {oldValues.Condition} → {asset.Condition}");

                if (oldValues.Location != asset.Location)
                    changes.Add($"Location: {oldValues.Location ?? "N/A"} → {asset.Location ?? "N/A"}");

                // =========================================
                // 13. SAVE HISTORY IF CHANGES EXIST
                // =========================================

                if (changes.Any())
                {
                    _db.AssetHistories.Add(new AssetHistory
                    {
                        AssetId = asset.Id,
                        Action = "Asset Updated",
                        OldValue = string.Join(" | ", changes),
                        NewValue = asset.AssetName,
                        Remarks = "Asset details updated from Asset Master.",
                        PerformedBy = currentUserId.Value,
                        PerformedDate = DateTime.Now
                    });
                }

                // =========================================
                // 14. SAVE
                // =========================================

                await _db.SaveChangesAsync();

                TempData["Success"] =
                    $"Asset '{asset.AssetName}' updated successfully.";

                return RedirectToAction("Assets");
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine("=================================");
                Console.WriteLine("EDIT ASSET DATABASE ERROR");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException?.Message);
                Console.WriteLine("=================================");

                TempData["Error"] =
                    $"Database error: {ex.InnerException?.Message ?? ex.Message}";

                return RedirectToAction("Assets");
            }
            catch (Exception ex)
            {
                Console.WriteLine("=================================");
                Console.WriteLine("EDIT ASSET ERROR");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("=================================");

                TempData["Error"] =
                    $"Error updating asset: {ex.Message}";

                return RedirectToAction("Assets");
            }
        }
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAsset(int id)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");

            if (currentUserId == null)
            {
                TempData["Error"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            var asset = await _db.Assets
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    !a.IsDeleted);

            if (asset == null)
            {
                TempData["Error"] = "Asset not found.";
                return RedirectToAction("Assets");
            }

            // -----------------------------------------
            // IMPORTANT
            // Don't allow deleting currently assigned asset
            // -----------------------------------------

            if (asset.CurrentEmployeeId != null ||
                asset.Status == AssetStatus.Assigned)
            {
                TempData["Error"] =
                    "This asset is currently assigned to an employee. " +
                    "Please return the asset before deactivating it.";

                return RedirectToAction("Assets");
            }

            // -----------------------------------------
            // Soft Delete
            // -----------------------------------------

            asset.IsActive = false;
            asset.IsDeleted = true;
            asset.UpdatedDate = DateTime.Now;

            // -----------------------------------------
            // History
            // -----------------------------------------

            _db.AssetHistories.Add(new AssetHistory
            {
                AssetId = asset.Id,
                Action = "Asset Deactivated",
                OldValue = asset.AssetName,
                NewValue = null,
                Remarks = "Asset marked inactive from Asset Master.",
                PerformedBy = currentUserId.Value,
                PerformedDate = DateTime.Now
            });

            await _db.SaveChangesAsync();

            TempData["Success"] =
                $"Asset '{asset.AssetName}' deactivated successfully.";

            return RedirectToAction("Assets");
        }

        // ================= ASSIGN / RETURN / HISTORY =================
        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> AssignAsset(int id)
        {
            var asset = await _db.Assets
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

            if (asset == null)
            {
                TempData["Error"] = "Asset not found.";
                return RedirectToAction("Assets");
            }

            // Provide list of active employees for selection
            ViewBag.Employees = await _db.Employees
                .Where(e => e.IsActive && !e.IsDeleted)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();

            return View(asset);
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignAsset(int assetId, int employeeId, string? handoverRemarks)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");

            if (currentUserId == null)
            {
                TempData["Error"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted);

            if (asset == null)
            {
                TempData["Error"] = "Asset not found.";
                return RedirectToAction("Assets");
            }

            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive && !e.IsDeleted);

            if (employee == null)
            {
                TempData["Error"] = "Selected employee not found or inactive.";
                return RedirectToAction("Assets");
            }

            // Prevent assigning if already assigned
            if (asset.CurrentEmployeeId != null || asset.Status == AssetStatus.Assigned)
            {
                TempData["Error"] = "This asset is already assigned. Please return it before assigning to another employee.";
                return RedirectToAction("Assets");
            }

            // Mark any previous assignments as not current (defensive)
            var previousAssignments = await _db.AssetAssignments
                .Where(asg => asg.AssetId == assetId && asg.IsCurrent)
                .ToListAsync();

            foreach (var p in previousAssignments)
            {
                p.IsCurrent = false;
            }

            var assignment = new AssetAssignment
            {
                AssetId = assetId,
                EmployeeId = employeeId,
                AssignedDate = DateTime.Now,
                AssignedBy = currentUserId.Value,
                IsCurrent = true,
                ConditionAtAssignment = asset.Condition,
                HandoverRemarks = handoverRemarks
            };

            _db.AssetAssignments.Add(assignment);

            // Update asset
            asset.CurrentEmployeeId = employeeId;
            asset.Status = AssetStatus.Assigned;
            asset.UpdatedDate = DateTime.Now;

            // History
            _db.AssetHistories.Add(new AssetHistory
            {
                AssetId = asset.Id,
                Action = "Assigned",
                OldValue = asset.AssetName,
                NewValue = employee.FirstName + " " + employee.LastName,
                Remarks = handoverRemarks ?? "",
                PerformedBy = currentUserId.Value,
                PerformedDate = DateTime.Now
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Asset '{asset.AssetName}' assigned to {employee.FirstName} {employee.LastName}.";
            return RedirectToAction("Assets");
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnAsset(int assignmentId, string? returnRemarks)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");

            if (currentUserId == null)
            {
                TempData["Error"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            var assignment = await _db.AssetAssignments
                .Include(a => a.Asset)
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.IsCurrent);

            if (assignment == null)
            {
                TempData["Error"] = "Active assignment not found.";
                return RedirectToAction("Assets");
            }

            assignment.ReturnedDate = DateTime.Now;
            assignment.ReturnedBy = currentUserId.Value;
            assignment.IsCurrent = false;
            assignment.ReturnRemarks = returnRemarks;
            assignment.ConditionAtReturn = assignment.ConditionAtReturn ?? assignment.ConditionAtAssignment;

            // Update asset
            var asset = assignment.Asset!;
            asset.CurrentEmployeeId = null;
            asset.Status = AssetStatus.Available;
            asset.UpdatedDate = DateTime.Now;

            // History
            _db.AssetHistories.Add(new AssetHistory
            {
                AssetId = asset.Id,
                Action = "Returned",
                OldValue = assignment.Employee?.FirstName + " " + assignment.Employee?.LastName,
                NewValue = asset.AssetName,
                Remarks = returnRemarks ?? "",
                PerformedBy = currentUserId.Value,
                PerformedDate = DateTime.Now
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Asset '{asset.AssetName}' returned successfully.";
            return RedirectToAction("Assets");
        }
        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> GetAssetHistory(int assetId)
        {
            var asset = await _db.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.Id == assetId &&
                    !a.IsDeleted);

            if (asset == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Asset not found."
                });
            }

            var history = await (
                from h in _db.AssetHistories.AsNoTracking()

                join u in _db.Users.AsNoTracking()
                    on h.PerformedBy equals u.Id into userGroup

                from u in userGroup.DefaultIfEmpty()

                where h.AssetId == assetId

                orderby h.PerformedDate descending

                select new
                {
                    id = h.Id,

                    performedDate =
                        h.PerformedDate.ToString("yyyy-MM-dd HH:mm"),

                    action = h.Action,

                    oldValue = h.OldValue,

                    newValue = h.NewValue,

                    remarks = h.Remarks,

                    performedBy = u != null
                        ? u.Name
                        : "System"
                }
            ).ToListAsync();

            return Json(new
            {
                success = true,

                assetName = asset.AssetName,
                assetCode = asset.AssetCode,

                history
            });
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAssetActive(int id)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null)
            {
                TempData["Error"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id);
            if (asset == null)
            {
                TempData["Error"] = "Asset not found.";
                return RedirectToAction("Assets");
            }

            asset.IsActive = !asset.IsActive;
            asset.IsDeleted = !asset.IsActive;
            asset.UpdatedDate = DateTime.Now;

            _db.AssetHistories.Add(new AssetHistory
            {
                AssetId = asset.Id,
                Action = asset.IsActive ? "Reactivated" : "Deactivated",
                OldValue = asset.AssetName,
                NewValue = asset.IsActive ? "Active" : "Inactive",
                Remarks = asset.IsActive ? "Asset reactivated by admin." : "Asset deactivated by admin.",
                PerformedBy = currentUserId.Value,
                PerformedDate = DateTime.Now
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = asset.IsActive ? "Asset reactivated." : "Asset deactivated.";
            return RedirectToAction("Assets");
        }

        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> EmployeeProfile(int id)
        {
            // Debug: log session values to troubleshoot unexpected redirects
            var sid = HttpContext.Session.GetInt32("UserId");
            var srole = HttpContext.Session.GetInt32("RoleId");
            var sname = HttpContext.Session.GetString("UserName");
            Console.WriteLine($"ADMIN EMP PROFILE REQUEST: Session UserId={sid}, RoleId={srole}, UserName={sname}");

            var employee = await _db.Employees
                .Include(e => e.User)
                    .ThenInclude(u => u!.Role)
                .Include(e => e.ReportingToUser)
                .Include(e => e.Educations)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

            if (employee == null)
            {
                TempData["Error"] = "Employee not found.";
                return RedirectToAction("Employees");
            }

            var documents = await _db.EmployeeDocuments
                .Where(d => d.EmployeeId == employee.Id)
                .OrderBy(d => d.DocumentType)
                .ToListAsync();

            ViewBag.Documents = documents;

            var assignedAssets = await _db.AssetAssignments
                .Where(a => a.EmployeeId == id && a.IsCurrent)
                .Include(a => a.Asset)
                .ToListAsync();

            ViewBag.AssignedAssets = assignedAssets;

            // available assets for admin to assign
            var availableAssets = await _db.Assets
                .Where(a => !a.IsDeleted && a.CurrentEmployeeId == null)
                .OrderBy(a => a.AssetName)
                .ToListAsync();

            ViewBag.AvailableAssets = availableAssets;

            return View("~/Views/Employee/Profile.cshtml", employee);
        }

        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> AssetCategories()
        {
            var categories = await _db.AssetCategories
                .Include(c => c.Assets)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAssetCategory(
    string name,
    string? description)
        {
            // -------------------------
            // Validate
            // -------------------------

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Category name is required.";
                return RedirectToAction("AssetCategories");
            }

            name = name.Trim();

            // -------------------------
            // Check duplicate
            // -------------------------

            bool exists = await _db.AssetCategories
                .AnyAsync(c =>
                    c.IsActive &&
                    c.Name.ToLower() == name.ToLower());

            if (exists)
            {
                TempData["Error"] = "Asset category already exists.";
                return RedirectToAction("AssetCategories");
            }

            // -------------------------
            // Logged-in user
            // -------------------------

            var currentUserId = HttpContext.Session.GetInt32("UserId");

            if (currentUserId == null)
            {
                TempData["Error"] =
                    "Your session has expired. Please login again.";

                return RedirectToAction("Login", "Account");
            }

            // -------------------------
            // Create category
            // -------------------------

            var category = new AssetCategory
            {
                Name = name,

                Description = string.IsNullOrWhiteSpace(description)
                    ? null
                    : description.Trim(),

                IsActive = true,

                CreatedBy = currentUserId.Value,

                CreatedDate = DateTime.Now
            };

            _db.AssetCategories.Add(category);

            await _db.SaveChangesAsync();

            TempData["Success"] =
                "Asset category added successfully.";

            return RedirectToAction("AssetCategories");
        }


        [AuthorizeAdmin]
        [HttpGet]
        public async Task<IActionResult> EditAssetCategory(int id)
        {
            var category = await _db.AssetCategories
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.IsActive);

            if (category == null)
            {
                TempData["Error"] = "Asset category not found.";
                return RedirectToAction("AssetCategories");
            }

            return View(category);
        }
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAssetCategory(
    AssetCategory model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var category = await _db.AssetCategories
                .FirstOrDefaultAsync(c =>
                    c.Id == model.Id &&
                    c.IsActive);

            if (category == null)
            {
                TempData["Error"] =
                    "Asset category not found.";

                return RedirectToAction("AssetCategories");
            }

            // Check duplicate category name
            bool duplicate = await _db.AssetCategories
                .AnyAsync(c =>
                    c.Id != model.Id &&
                    c.IsActive &&
                    c.Name.ToLower() == model.Name.Trim().ToLower());

            if (duplicate)
            {
                TempData["Error"] =
                    "Another asset category with this name already exists.";

                return RedirectToAction("AssetCategories");
            }

            category.Name = model.Name.Trim();

            category.Description =
                string.IsNullOrWhiteSpace(model.Description)
                    ? null
                    : model.Description.Trim();

            await _db.SaveChangesAsync();

            TempData["Success"] =
                "Asset category updated successfully.";

            return RedirectToAction("AssetCategories");
        }
        [AuthorizeAdmin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAssetCategory(int id)
        {
            var category = await _db.AssetCategories
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.IsActive);

            if (category == null)
            {
                TempData["Error"] =
                    "Asset category not found.";

                return RedirectToAction("AssetCategories");
            }

            // Check whether assets are using this category
            bool hasAssets = await _db.Assets
                .AnyAsync(a =>
                    a.AssetCategoryId == id &&
                    !a.IsDeleted);

            if (hasAssets)
            {
                TempData["Error"] =
                    "This category cannot be deleted because assets are already assigned to it.";

                return RedirectToAction("AssetCategories");
            }

            category.IsActive = false;

            await _db.SaveChangesAsync();

            TempData["Success"] =
                "Asset category deleted successfully.";

            return RedirectToAction("AssetCategories");
        }

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


