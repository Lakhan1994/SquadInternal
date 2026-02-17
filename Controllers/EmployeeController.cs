using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using SquadInternal.Data;
using SquadInternal.Filters;
using SquadInternal.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SquadInternal.Controllers
{
    [SessionAuthorize(SessionAuthMode.EmployeeOnly)]
    public class EmployeeController : Controller
    {
        private readonly AppDbContext _db;

        public EmployeeController(AppDbContext db)
        {
            _db = db;
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

        // ================= APPLY LEAVE PAGE =================
        public IActionResult ApplyLeave()
        {
            return View();
        }

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

            return Json(new
            {
                success = true,
                message = "Leave applied successfully."
            });
        }



        // ================= CANCEL / BACKOUT LEAVE =================
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

            // Only allow cancel if still pending
            if (leave.Status == "Pending")
            {
                leave.Status = "Cancelled";
                await _db.SaveChangesAsync();
                TempData["Success"] = "Leave cancelled successfully.";
            }

            return RedirectToAction("Leave");
        }
    }
}
