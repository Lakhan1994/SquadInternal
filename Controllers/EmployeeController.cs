using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Filters;
using SquadInternal.Models;
using SquadInternal.Services;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace SquadInternal.Controllers
{
    [SessionAuthorize(SessionAuthMode.EmployeeOnly)]
    public class EmployeeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly EmailService _emailService;
        private readonly AzureBlobStorageService _blobStorageService;
        private readonly AnnualLeavePolicyService _leavePolicyService;
       

        public EmployeeController(AppDbContext db, EmailService emailService, AzureBlobStorageService blobStorageService, AnnualLeavePolicyService leavePolicyService)
        {
            _db = db;
            _emailService = emailService;
            _blobStorageService = blobStorageService;
            _leavePolicyService = leavePolicyService;
           
        }

        // ================= MY PROFILE =================
        public async Task<IActionResult> Profile(int? id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var roleId = HttpContext.Session.GetInt32("RoleId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            Employee? employee = null;

            // If admin requested a specific employee id, allow viewing that profile
            if (id.HasValue && roleId == 1)
            {
                employee = await _db.Employees
                    .Include(e => e.User)
                        .ThenInclude(u => u!.Role)
                    .Include(e => e.ReportingToUser)
                    .Include(e => e.Educations)
                    .FirstOrDefaultAsync(e => e.Id == id.Value && !e.IsDeleted);
            }

            // Otherwise show current user's profile
            if (employee == null)
            {
                employee = await _db.Employees
                    .Include(e => e.User)
                        .ThenInclude(u => u!.Role)
                    .Include(e => e.ReportingToUser)
                    .Include(e => e.Educations)
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);
            }

            if (employee == null)
                return Content("Employee record not found. Please contact admin.");

            var documents = await _db.EmployeeDocuments
                .Where(d => d.EmployeeId == employee.Id)
                .OrderBy(d => d.DocumentType)
                .ToListAsync();

            ViewBag.Documents = documents;

            // Assigned assets for this employee (current assignments)
            var assignedAssets = await _db.AssetAssignments
                .Where(a => a.EmployeeId == employee.Id && a.IsCurrent)
                .Include(a => a.Asset)
                .OrderByDescending(a => a.AssignedDate)
                .ToListAsync();

            ViewBag.AssignedAssets = assignedAssets;

            return View(employee);
        }

        // ================= DOWNLOAD MY DOCUMENT =================
        public async Task<IActionResult> DownloadMyDocument(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null)
                return Content("Employee record not found.");

            var document = await _db.EmployeeDocuments
                .FirstOrDefaultAsync(d => d.Id == id && d.EmployeeId == employee.Id);

            if (document == null)
                return NotFound();

            var (content, contentType) = await _blobStorageService.DownloadAsync(document.FilePath);

            return File(content, contentType, document.DocumentName);
        }

        // ================= MY PROFILE PICTURE =================
        public async Task<IActionResult> GetProfilePicture()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null || string.IsNullOrWhiteSpace(employee.ProfilePicturePath))
                return NotFound();

            var (content, contentType) = await _blobStorageService.DownloadAsync(employee.ProfilePicturePath);

            Response.Headers["Cache-Control"] = "no-cache";
            return File(content, contentType);
        }

        
        // ================= MY LEAVES =================
        public async Task<IActionResult> Leave()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null)
                return Content("Employee record not found. Please contact admin.");

            var leaves = await _db.EmployeeLeaves
                .Include(leave => leave.ApprovedByUser)
                .Where(l => l.EmployeeId == employee.Id)
                .OrderByDescending(l => l.AppliedOn)
                .ToListAsync();

            var holidayRows = await _db.SquadHolidays
                .Where(h => h.IsActive)
                .Select(h => new { h.HolidayDate, h.IsHalfDay })
                .ToListAsync();
            var holidayMap = holidayRows
                .GroupBy(h => h.HolidayDate.Date)
                .ToDictionary(group => group.Key, group => group.All(h => h.IsHalfDay));
            ViewBag.HolidayMap = holidayMap;
            ViewBag.HolidayCalendar = holidayMap.ToDictionary(
                holiday => holiday.Key.ToString("yyyy-MM-dd"),
                holiday => holiday.Value);

            var leaveYear = DateTime.Today.Year;
            var policy = await _leavePolicyService.GetPolicyAsync(leaveYear);
            var casualBalance = policy == null ? null : await _leavePolicyService.GetOrCreateBalanceAsync(employee, leaveYear, "Casual Leave");
            var sickBalance = policy == null ? null : await _leavePolicyService.GetOrCreateBalanceAsync(employee, leaveYear, "Sick Leave");
            var earnedBalance = policy?.EarnedLeaveEnabled == true
                ? await _leavePolicyService.GetOrCreateBalanceAsync(employee, leaveYear, "Earned Leave")
                : null;
            ViewBag.LeaveYear = leaveYear;
            ViewBag.PolicyActive = policy != null;
            ViewBag.EarnedLeaveEnabled = policy?.EarnedLeaveEnabled ?? false;
            ViewBag.CasualAvailable = casualBalance == null ? 0m : await _leavePolicyService.GetAvailableBalanceAsync(casualBalance);
            ViewBag.SickAvailable = sickBalance == null ? 0m : await _leavePolicyService.GetAvailableBalanceAsync(sickBalance);
            ViewBag.EarnedAvailable = earnedBalance == null ? 0m : await _leavePolicyService.GetAvailableBalanceAsync(earnedBalance);
            ViewBag.AnnualEntitlement =
                (casualBalance?.AnnualEntitlement + casualBalance?.ProratedEntitlement ?? 0m) +
                (sickBalance?.AnnualEntitlement + sickBalance?.ProratedEntitlement ?? 0m) +
                (earnedBalance?.AnnualEntitlement + earnedBalance?.ProratedEntitlement ?? 0m);
            ViewBag.SeparateBonusCategory = policy?.BonusUsesSeparateCategory ?? false;
            ViewBag.BonusDays = await _db.EmployeeLeaveBalances
                .Where(balance => balance.EmployeeId == employee.Id && balance.LeaveYear == leaveYear)
                .SumAsync(balance => (decimal?)balance.BonusCredits) ?? 0m;
            ViewBag.PendingRequests = leaves.Count(leave => leave.Status == "Pending");

            return View(leaves);
        }

        // ================= APPLY LEAVE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyLeave(EmployeeLeave model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return Json(new { success = false, message = "Session expired." });

            if (model.ToDate < model.FromDate)
                return Json(new { success = false, message = "To Date cannot be earlier than From Date." });

            if (model.FromDate.Date < DateTime.Today)
                return Json(new { success = false, message = "Leave cannot be applied for past dates." });

            var allowedLeaveTypes = new[]
            {
                "Sick Leave", "Casual Leave", "Earned Leave", "Privilege Leave",
                "Paternity Leave", "Maternity Leave"
            };
            if (!allowedLeaveTypes.Contains(model.LeaveType, StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Select a valid leave type." });

            if (model.Duration is not ("Full Day" or "First Half" or "Second Half"))
                return Json(new { success = false, message = "Select a valid leave duration." });

            if (model.Duration != "Full Day" && model.FromDate.Date != model.ToDate.Date)
                return Json(new { success = false, message = "Multi-day requests must use Full Day duration." });

            if (string.IsNullOrWhiteSpace(model.Reason))
                return Json(new { success = false, message = "Please enter a reason for your leave." });

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null)
                return Json(new { success = false, message = "Employee not found." });

            var annualLeaveType = AnnualLeavePolicyService.NormalizeAnnualLeaveType(model.LeaveType);
            var policy = await _leavePolicyService.GetPolicyAsync(model.FromDate.Year);
            if (annualLeaveType == "Earned Leave" && model.FromDate.Year >= 2027 && (policy == null || !policy.EarnedLeaveEnabled))
                return Json(new { success = false, message = "Earned/Privilege Leave is not currently configured." });
            if (annualLeaveType == "Bonus Leave" && model.FromDate.Year >= 2027 && (policy == null || !policy.BonusUsesSeparateCategory))
                return Json(new { success = false, message = "Bonus Leave is not configured as a separate category." });
            if (annualLeaveType != null && model.FromDate.Year >= 2027 && model.FromDate.Year != model.ToDate.Year)
                return Json(new { success = false, message = "Annual leave requests must be within one leave year." });

            var holidayRows = await _db.SquadHolidays
                .Where(h => h.IsActive && h.HolidayDate.Date >= model.FromDate.Date && h.HolidayDate.Date <= model.ToDate.Date)
                .Select(h => new { h.HolidayDate, h.IsHalfDay })
                .ToListAsync();
            var holidays = holidayRows
                .GroupBy(h => h.HolidayDate.Date)
                .ToDictionary(group => group.Key, group => group.All(h => h.IsHalfDay));
            var leaveDays = LeaveDayCalculator.Calculate(model.FromDate, model.ToDate, model.Duration, holidays);
            if (leaveDays <= 0m)
                return Json(new { success = false, message = "The selected dates contain no working leave days." });

            var executionStrategy = _db.Database.CreateExecutionStrategy();
            var transactionFailure = await executionStrategy.ExecuteAsync<IActionResult?>(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

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
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = "You already have leave applied for selected date(s)."
                    });
                }

                if (annualLeaveType is "Casual Leave" or "Sick Leave" or "Earned Leave" or "Bonus Leave" && model.FromDate.Year >= 2027)
                {
                    if (policy == null)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = "The annual leave policy is not configured for this year." });
                    }

                    var balance = await _leavePolicyService.GetOrCreateBalanceAsync(employee, model.FromDate.Year, annualLeaveType);
                    if (balance == null)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = "No balance is configured for this leave category." });
                    }

                    var available = await _leavePolicyService.GetAvailableBalanceAsync(balance);
                    if (available < leaveDays)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = $"Insufficient {annualLeaveType} balance. Available: {available:0.##} days." });
                    }
                }

                model.EmployeeId = employee.Id;
                model.Status = "Pending";
                model.AppliedOn = DateTime.Now;

                _db.EmployeeLeaves.Add(model);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return null;
            });

            if (transactionFailure != null)
                return transactionFailure;

            var admin = await _db.Users.FirstOrDefaultAsync(u => u.RoleId == 1);

            if (admin != null)
            {
                try
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var days = leaveDays;

                    var emailBody = _emailService.GetAdminLeaveTemplate(
                        employee.FirstName + " " + employee.LastName,
                        model.FromDate,
                        model.ToDate,
                        model.LeaveType,
                        model.Reason,
                        model.Id,
                        baseUrl,
                        admin.Name
                    );

                    string subject = $"Leave Request: {employee.FirstName} {employee.LastName} - {days:0.##} Days";

                    _emailService.SendEmail(admin.Email, subject, emailBody);
                }
                catch
                {
                    return Json(new
                    {
                        success = false,
                        message = "Leave applied but email notification failed. Please contact admin."
                    });
                }
            }

            return Json(new
            {
                success = true,
                message = $"Leave applied successfully ({leaveDays:0.##} working days). Notification sent to admin.",
                leaveDays
            });
        }

        // ================= CANCEL LEAVE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelLeave(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null)
                return RedirectToAction("Leave");

            var leave = await _db.EmployeeLeaves
                .FirstOrDefaultAsync(l => l.Id == id && l.EmployeeId == employee.Id);

            if (leave == null)
                return RedirectToAction("Leave");

            if (leave.Status != "Pending")
            {
                TempData["Error"] = "Only pending leaves can be cancelled.";
                return RedirectToAction("Leave");
            }

            leave.Status = "Cancelled";
            leave.CancelledByUserId = userId;
            leave.CancelledOn = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var admin = await _db.Users.FirstOrDefaultAsync(u => u.RoleId == 1);

            if (admin != null)
            {
                try
                {
                    var body = _emailService.GetLeaveCancelledTemplate(
                        employee.FirstName + " " + employee.LastName,
                        leave.FromDate,
                        leave.ToDate,
                        leave.LeaveType,
                        leave.Reason,
                        admin.Name
                    );

                    _emailService.SendEmail(admin.Email, "Leave Cancelled", body);
                }
                catch
                {
                    // Do not block user if email fails
                }
            }

            TempData["Success"] = "Leave cancelled successfully.";
            return RedirectToAction("Leave");
        }
    }
}