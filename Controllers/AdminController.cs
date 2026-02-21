using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Filters;
using SquadInternal.Models;
using SquadInternal.Models.ViewModels;
using SquadInternal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;


namespace SquadInternal.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PasswordService _passwordService;

        public AdminController(AppDbContext db, PasswordService passwordService)
        {
            _db = db;
            _passwordService = passwordService;
        }

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
            ViewBag.EmployeeCount = await _db.Employees.CountAsync(e => e.IsActive);
            ViewBag.RoleCount = await _db.Roles.CountAsync(r => r.IsActive);

            ViewBag.Holidays = await _db.SquadHolidays
                .Where(h => h.IsActive)
                .Select(h => h.HolidayDate.Date)
                .ToListAsync();

            ViewBag.Today = DateTime.Today;

            var employee = await GetCurrentEmployeeAsync();

            if (employee != null)
            {
                var approvedLeaves = await _db.EmployeeLeaves
                    .Where(l => l.EmployeeId == employee.Id && l.Status == "Approved")
                    .ToListAsync();

                ViewBag.ApprovedLeaveDates = approvedLeaves
                    .SelectMany(l => ExpandDates(l.FromDate, l.ToDate))
                    .ToList();
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
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid data submitted.";
                return RedirectToAction("Employees");
            }

            if (await _db.Users.AnyAsync(u => u.Email == model.Email && u.IsActive))
            {
                TempData["Error"] = "Email already exists.";
                return RedirectToAction("Employees");
            }

            // Create User
            var user = new User
            {
                Name = model.FirstName + " " + model.LastName,
                Email = model.Email,
                RoleId = model.RoleId,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Create Employee
            var employee = new Employee
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                UserId = user.Id,
                ReportingToUserId = model.ReportingToUserId,
                DateOfBirth = model.DateOfBirth,
                DateOfJoining = model.DateOfJoining,
                Gender = model.Gender,
                IsActive = true,
                IsDeleted = false,
                AddedDate = DateTime.Now
            };

            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();

            // Save Educations
            if (model.Educations != null && model.Educations.Any())
            {
                foreach (var edu in model.Educations)
                {
                    _db.EmployeeEducations.Add(new EmployeeEducation
                    {
                        EmployeeId = employee.Id,
                        Level = edu.Level,
                        Stream = edu.Stream,
                        Institute = edu.Institute,
                        PassingYear = edu.PassingYear,
                        PercentageOrCGPA = edu.PercentageOrCGPA
                    });
                }

                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Employee created successfully.";
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
            if (leave == null) return RedirectToAction("LeaveApprovals");

            leave.Status = "Approved";
            leave.ApprovedByUserId = HttpContext.Session.GetInt32("UserId");
            await _db.SaveChangesAsync();

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

        public async Task<IActionResult> LeaveManagement(int? employeeId)
        {
            var leavesQuery = _db.EmployeeLeaves
                .Include(l => l.Employee)
                .AsQueryable();

            var employees = await _db.Employees
                .Where(e => e.IsActive)
                .ToListAsync();

            ViewBag.EmployeeList = employees;
            ViewBag.SelectedEmployeeId = employeeId;

            if (employeeId.HasValue)
            {
                leavesQuery = leavesQuery.Where(l => l.EmployeeId == employeeId);
            }

            var leaves = await leavesQuery.ToListAsync();

            if (employeeId.HasValue)
            {
                ViewBag.TotalLeaves = leaves.Count;
                ViewBag.ApprovedCount = leaves.Count(l => l.Status == "Approved");
                ViewBag.PendingCount = leaves.Count(l => l.Status == "Pending");
                ViewBag.RejectedCount = leaves.Count(l => l.Status == "Rejected");
            }

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
        [HttpGet]
        public IActionResult BulkUploadHolidays()
        {
            return View();
        }

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

            // For now just confirm upload (real Excel parsing can be added later)
            TempData["Success"] = "File uploaded successfully (processing not implemented yet).";
            return RedirectToAction("BulkUploadHolidays");
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

    }
}


