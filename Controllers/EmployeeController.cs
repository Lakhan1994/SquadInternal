using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Filters;
using SquadInternal.Models;
using SquadInternal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SquadInternal.Controllers
{
    [SessionAuthorize(SessionAuthMode.EmployeeOnly)]
    public class EmployeeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly EmailService _emailService;

        public EmployeeController(AppDbContext db, EmailService emailService)
        {
            _db = db;
            _emailService = emailService;
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
                .Where(l => l.EmployeeId == employee.Id)
                .OrderByDescending(l => l.AppliedOn)
                .ToListAsync();

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

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);

            if (employee == null)
                return Json(new { success = false, message = "Employee not found." });

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
                return Json(new
                {
                    success = false,
                    message = "You already have leave applied for selected date(s)."
                });
            }

            model.EmployeeId = employee.Id;
            model.Status = "Pending";
            model.AppliedOn = DateTime.Now;

            _db.EmployeeLeaves.Add(model);
            await _db.SaveChangesAsync();

            var admin = await _db.Users.FirstOrDefaultAsync(u => u.RoleId == 1);

            if (admin != null)
            {
                try
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var days = (model.ToDate - model.FromDate).Days + 1;

                    var emailBody = _emailService.GetAdminLeaveTemplate(
                        employee.FirstName + " " + employee.LastName,
                        model.FromDate,
                        model.ToDate,
                        model.LeaveType,
                        model.Reason,
                        model.Id,
                        baseUrl
                    );

                    string subject = days > 1
                        ? $"✈️ Leave Request: {employee.FirstName} {employee.LastName} - {days} Days"
                        : $"✈️ Leave Request: {employee.FirstName} {employee.LastName} - {days} Day";

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
                message = "Leave applied successfully. Notification sent to admin."
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
                        leave.Reason
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