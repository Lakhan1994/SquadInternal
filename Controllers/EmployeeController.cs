using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Filters;
using SquadInternal.Models;
using SquadInternal.Services;
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

            // Set values
            model.EmployeeId = employee.Id;
            model.Status = "Pending";
            model.AppliedOn = DateTime.Now;

            _db.EmployeeLeaves.Add(model);
            await _db.SaveChangesAsync();

            // Fetch Admin
            var admin = await _db.Users.FirstOrDefaultAsync(u => u.RoleId == 1);

            if (admin != null)
            {
                try
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var currentYear = DateTime.Now.Year;

                    // Calculate number of days
                    var days = (model.ToDate - model.FromDate).Days + 1;
                    var dateRange = model.FromDate.ToString("MMMM dd, yyyy");
                    if (model.FromDate.Date != model.ToDate.Date)
                        dateRange = $"{model.FromDate:MMMM dd, yyyy} - {model.ToDate:MMMM dd, yyyy}";

                    // Get user email from the User table
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                    var userEmail = user?.Email ?? "No email on file";

                    var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0; padding:0; font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color:#f5f5f5;'>
    <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background-color:#f5f5f5; padding:30px 0;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' border='0' style='background-color:#ffffff; border-radius:8px; box-shadow:0 2px 10px rgba(0,0,0,0.08);'>
                    
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px 40px; border-radius: 8px 8px 0 0;'>
                            <h1 style='color:#ffffff; margin:0; font-size:24px; font-weight:500;'>Leave Application</h1>
                            <p style='color:#e0e0e0; margin:5px 0 0 0; font-size:14px;'>New leave request requires your review</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            
                            <!-- Status Badge -->
                            <table style='background-color:#FFF3E0; border-radius:20px; margin-bottom:25px;'>
                                <tr>
                                    <td style='padding:8px 20px;'>
                                        <span style='color:#F57C00; font-weight:600; font-size:14px;'>⏳ PENDING REVIEW</span>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Employee Info Card -->
                            <table width='100%' style='background-color:#F8F9FA; border-radius:8px; margin-bottom:25px;'>
                                <tr>
                                    <td style='padding:20px;'>
                                        <h2 style='margin:0 0 10px 0; font-size:16px; color:#495057; font-weight:600; text-transform:uppercase; letter-spacing:0.5px;'>Employee Information</h2>
                                        <table width='100%'>
                                            <tr>
                                                <td width='80' style='padding:5px 0; color:#6C757D;'>Name:</td>
                                                <td style='padding:5px 0; color:#212529; font-weight:500;'>{employee.FirstName} {employee.LastName}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:5px 0; color:#6C757D;'>Email:</td>
                                                <td style='padding:5px 0; color:#212529;'>{userEmail}</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Leave Details Card -->
                            <table width='100%' style='background-color:#F8F9FA; border-radius:8px; margin-bottom:25px;'>
                                <tr>
                                    <td style='padding:20px;'>
                                        <h2 style='margin:0 0 10px 0; font-size:16px; color:#495057; font-weight:600; text-transform:uppercase; letter-spacing:0.5px;'>Leave Details</h2>
                                        <table width='100%'>
                                            <tr>
                                                <td width='100' style='padding:5px 0; color:#6C757D;'>Duration:</td>
                                                <td style='padding:5px 0; color:#212529; font-weight:500;'>{dateRange}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:5px 0; color:#6C757D;'>Days:</td>
                                                <td style='padding:5px 0; color:#212529;'><span style='background-color:#E3F2FD; color:#1976D2; padding:4px 12px; border-radius:16px; font-weight:500;'>{days} day{(days > 1 ? "s" : "")}</span></td>
                                            </tr>
                                            <tr>
                                                <td style='padding:5px 0; color:#6C757D; vertical-align:top;'>Reason:</td>
                                                <td style='padding:5px 0; color:#212529;'>{model.Reason}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:5px 0; color:#6C757D;'>Applied:</td>
                                                <td style='padding:5px 0; color:#212529;'>{model.AppliedOn:MMMM dd, yyyy 'at' h:mm tt}</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Action Buttons -->
                            <table width='100%' cellpadding='0' cellspacing='0'>
                                <tr>
                                    <td align='center'>
                                        <table cellpadding='0' cellspacing='0'>
                                            <tr>
                                                <td style='padding-right:10px;'>
                                                    <a href='{baseUrl}/Admin/ApproveFromEmail?id={model.Id}' 
                                                       style='background-color:#28A745; 
                                                              color:#ffffff; 
                                                              padding:14px 32px; 
                                                              text-decoration:none; 
                                                              border-radius:6px;
                                                              font-weight:500;
                                                              font-size:15px;
                                                              display:inline-block;
                                                              border:1px solid #218838;
                                                              box-shadow:0 2px 4px rgba(40, 167, 69, 0.2);'>
                                                       ✓ APPROVE
                                                    </a>
                                                </td>
                                                <td>
                                                    <a href='{baseUrl}/Admin/RejectFromEmail?id={model.Id}' 
                                                       style='background-color:#DC3545; 
                                                              color:#ffffff; 
                                                              padding:14px 32px; 
                                                              text-decoration:none; 
                                                              border-radius:6px;
                                                              font-weight:500;
                                                              font-size:15px;
                                                              display:inline-block;
                                                              border:1px solid #C82333;
                                                              box-shadow:0 2px 4px rgba(220, 53, 69, 0.2);'>
                                                       ✗ REJECT
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Quick Response Note -->
                            <table style='margin-top:25px; padding-top:20px; border-top:1px solid #E9ECEF;'>
                                <tr>
                                    <td style='color:#6C757D; font-size:13px; line-height:1.5; text-align:center;'>
                                        <span style='display:inline-block; background-color:#E9ECEF; border-radius:16px; padding:5px 15px;'>⚡ Quick response ensures smooth workflow</span>
                                    </td>
                                </tr>
                            </table>
                            
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color:#F8F9FA; padding:20px 40px; border-radius:0 0 8px 8px; border-top:1px solid #E9ECEF;'>
                            <table width='100%'>
                                <tr>
                                    <td align='left' style='color:#6C757D; font-size:13px;'>
                                        <span style='font-weight:600; color:#495057;'>SquadInternal HR System</span><br>
                                        This is an automated notification. Please review the leave request at your earliest convenience.
                                    </td>
                                    <td align='right' style='color:#ADB5BD; font-size:12px;'>
                                        © {currentYear} All Rights Reserved
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                </table>
                
                <!-- Hidden Preheader -->
                <div style='display:none; font-size:1px; color:#f5f5f5; line-height:1px; max-height:0px; max-width:0px; opacity:0; overflow:hidden;'>
                    Leave request from {employee.FirstName} {employee.LastName} - {days} day{(days > 1 ? "s" : "")}
                </div>
                
            </td>
        </tr>
    </table>
</body>
</html>";

                    _emailService.SendEmail(admin.Email, $"Leave Request: {employee.FirstName} {employee.LastName} - {dateRange}", emailBody);
                }
                catch (Exception ex)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Email failed: " + ex.Message
                    });
                }
            }

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