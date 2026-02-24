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

                    // Get initials safely
                    string firstNameInitial = !string.IsNullOrEmpty(employee.FirstName) && employee.FirstName.Length > 0
                        ? employee.FirstName.Substring(0, 1)
                        : "E";
                    string lastNameInitial = !string.IsNullOrEmpty(employee.LastName) && employee.LastName.Length > 0
                        ? employee.LastName.Substring(0, 1)
                        : "M";

                    var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Leave Application Notification</title>
</head>
<body style='margin:0; padding:0; font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding:30px 0;'>
    <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background: transparent; padding:30px 0;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' border='0' style='background-color:#ffffff; border-radius:12px; box-shadow:0 20px 40px rgba(0,0,0,0.15); overflow:hidden;'>
                    
                    <!-- Animated Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #4776E6 0%, #8E54E9 100%); padding: 35px 40px; position:relative;'>
                            <div style='position:absolute; top:0; right:0; opacity:0.1;'>
                                <svg width='120' height='120' viewBox='0 0 24 24' fill='none' xmlns='http://www.w3.org/2000/svg'>
                                    <path d='M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z' fill='white'/>
                                </svg>
                            </div>
                            <h1 style='color:#ffffff; margin:0; font-size:28px; font-weight:600; letter-spacing:-0.5px;'>New Leave Request</h1>
                            <p style='color:#e0e0e0; margin:8px 0 0 0; font-size:16px; opacity:0.9;'>Action required within 24-48 hours</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            
                            <!-- Status Timeline -->
                            <table width='100%' style='margin-bottom:30px;'>
                                <tr>
                                    <td align='center'>
                                        <table cellpadding='0' cellspacing='0'>
                                            <tr>
                                                <td style='background-color:#E3F2FD; border-radius:30px; padding:12px 30px;'>
                                                    <span style='color:#1976D2; font-weight:600; font-size:15px; text-transform:uppercase; letter-spacing:1px;'>
                                                        ⏳ Pending Review
                                                    </span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Employee Profile Card -->
                            <table width='100%' style='background: linear-gradient(145deg, #f8faff 0%, #f0f4ff 100%); border-radius:16px; margin-bottom:25px; border-left:4px solid #4776E6;'>
                                <tr>
                                    <td style='padding:25px;'>
                                        <div style='display:flex; align-items:center; margin-bottom:15px;'>
                                            <div style='width:50px; height:50px; background: linear-gradient(135deg, #4776E6 0%, #8E54E9 100%); border-radius:50%; display:flex; align-items:center; justify-content:center; margin-right:15px;'>
                                                <span style='color:white; font-size:20px; font-weight:600;'>{firstNameInitial}{lastNameInitial}</span>
                                            </div>
                                            <div>
                                                <h3 style='margin:0; color:#2C3E50; font-size:18px; font-weight:600;'>{employee.FirstName} {employee.LastName}</h3>
                                                <p style='margin:5px 0 0 0; color:#7F8C8D; font-size:14px;'>{userEmail}</p>
                                            </div>
                                        </div>
                                        <table width='100%' style='margin-top:15px;'>
                                            <tr>
                                                <td width='50%' style='padding:8px 0;'>
                                                    <span style='color:#7F8C8D; font-size:13px;'>Employee ID</span><br>
                                                    <span style='color:#2C3E50; font-weight:500; font-size:15px;'>EMP-{employee.Id.ToString().PadLeft(4, '0')}</span>
                                                </td>
                                                <td style='padding:8px 0;'>
                                                    <span style='color:#7F8C8D; font-size:13px;'>Department</span><br>
                                                    <span style='color:#2C3E50; font-weight:500; font-size:15px;'>General</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Leave Details Card -->
                            <table width='100%' style='background-color:#ffffff; border:1px solid #E9ECEF; border-radius:16px; margin-bottom:25px; box-shadow:0 4px 6px rgba(0,0,0,0.02);'>
                                <tr>
                                    <td style='padding:25px;'>
                                        <h2 style='margin:0 0 20px 0; font-size:16px; color:#2C3E50; font-weight:600; text-transform:uppercase; letter-spacing:0.5px; border-bottom:2px solid #E9ECEF; padding-bottom:12px;'>
                                            📋 Leave Details
                                        </h2>
                                        <table width='100%'>
                                            <tr>
                                                <td width='40%' style='padding:10px 0; color:#7F8C8D; font-size:14px;'>Leave Period</td>
                                                <td style='padding:10px 0; color:#2C3E50; font-weight:500; font-size:15px;'>{dateRange}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0; color:#7F8C8D; font-size:14px;'>Duration</td>
                                                <td style='padding:10px 0;'>
                                                    <span style='background: linear-gradient(135deg, #4776E6 0%, #8E54E9 100%); color:#ffffff; padding:6px 16px; border-radius:30px; font-weight:500; font-size:14px; box-shadow:0 4px 10px rgba(71, 118, 230, 0.3);'>
                                                        {days} Day{(days > 1 ? "s" : "")}
                                                    </span>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0; color:#7F8C8D; font-size:14px; vertical-align:top;'>Reason</td>
                                                <td style='padding:10px 0; color:#2C3E50; font-size:15px; line-height:1.5; background-color:#F8F9FA; border-radius:8px; padding:12px;'>{model.Reason}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0; color:#7F8C8D; font-size:14px;'>Applied On</td>
                                                <td style='padding:10px 0; color:#2C3E50; font-size:14px;'>{model.AppliedOn:MMMM dd, yyyy} at {model.AppliedOn:h:mm tt}</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Action Buttons with Animation -->
                            <table width='100%' cellpadding='0' cellspacing='0'>
                                <tr>
                                    <td align='center'>
                                        <table cellpadding='0' cellspacing='0' style='margin:0 auto;'>
                                            <tr>
                                                <td style='padding:0 10px;'>
                                                    <a href='{baseUrl}/Admin/ApproveFromEmail?id={model.Id}' 
                                                       style='background: linear-gradient(135deg, #43C6AC 0%, #28A745 100%);
                                                              color:#ffffff; 
                                                              padding:16px 36px; 
                                                              text-decoration:none; 
                                                              border-radius:50px;
                                                              font-weight:600;
                                                              font-size:15px;
                                                              display:inline-block;
                                                              border:none;
                                                              box-shadow:0 8px 20px rgba(67, 198, 172, 0.4);
                                                              transition: transform 0.2s, box-shadow 0.2s;
                                                              letter-spacing:0.5px;'
                                                       onmouseover='this.style.transform=''translateY(-2px)''; this.style.boxShadow=''0 12px 25px rgba(67, 198, 172, 0.5)'';'
                                                       onmouseout='this.style.transform=''translateY(0)''; this.style.boxShadow=''0 8px 20px rgba(67, 198, 172, 0.4)'';'>
                                                        ✓ APPROVE LEAVE
                                                    </a>
                                                </td>
                                                <td style='padding:0 10px;'>
                                                    <a href='{baseUrl}/Admin/RejectFromEmail?id={model.Id}' 
                                                       style='background: linear-gradient(135deg, #FF6B6B 0%, #DC3545 100%);
                                                              color:#ffffff; 
                                                              padding:16px 36px; 
                                                              text-decoration:none; 
                                                              border-radius:50px;
                                                              font-weight:600;
                                                              font-size:15px;
                                                              display:inline-block;
                                                              border:none;
                                                              box-shadow:0 8px 20px rgba(220, 53, 69, 0.4);
                                                              transition: transform 0.2s, box-shadow 0.2s;
                                                              letter-spacing:0.5px;'
                                                       onmouseover='this.style.transform=''translateY(-2px)''; this.style.boxShadow=''0 12px 25px rgba(220, 53, 69, 0.5)'';'
                                                       onmouseout='this.style.transform=''translateY(0)''; this.style.boxShadow=''0 8px 20px rgba(220, 53, 69, 0.4)'';'>
                                                        ✗ REJECT LEAVE
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Quick Response Reminder -->
                            <table width='100%' style='margin-top:30px;'>
                                <tr>
                                    <td align='center'>
                                        <table style='background-color:#F1F8E9; border-radius:50px; padding:10px 25px;'>
                                            <tr>
                                                <td>
                                                    <span style='color:#558B2F; font-size:13px; font-weight:500;'>⚡ Quick response helps maintain team productivity</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                        </td>
                    </tr>
                    
                    <!-- Professional Footer -->
                    <tr>
                        <td style='background: linear-gradient(145deg, #2C3E50 0%, #1E2A3A 100%); padding:25px 40px;'>
                            <table width='100%'>
                                <tr>
                                    <td align='left' style='color:#BDC3C7; font-size:13px; line-height:1.6;'>
                                        <span style='font-weight:600; color:#ECF0F1; font-size:14px;'>SquadInternal HR System</span><br>
                                        <span style='opacity:0.8;'>This is an automated notification from the HR management system.</span><br>
                                        <span style='opacity:0.6;'>© {currentYear} All Rights Reserved</span>
                                    </td>
                                    <td align='right'>
                                        <span style='color:#95A5A6; font-size:12px;'>v2.0 | Secure HR Portal</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                </table>
                
                <!-- Hidden Preheader for Email Clients -->
                <div style='display:none; font-size:1px; color:#ffffff; line-height:1px; max-height:0px; max-width:0px; opacity:0; overflow:hidden;'>
                    ✨ {employee.FirstName} {employee.LastName} has requested {days} day{(days > 1 ? "s" : "")} of leave starting {model.FromDate:MMMM dd, yyyy}. Please review at your earliest convenience.
                </div>
                
            </td>
        </tr>
    </table>
</body>
</html>";

                    string subject = days > 1
                        ? $"✈️ Leave Request: {employee.FirstName} {employee.LastName} - {days} Days"
                        : $"✈️ Leave Request: {employee.FirstName} {employee.LastName} - {days} Day";

                    _emailService.SendEmail(admin.Email, subject, emailBody);
                }
                catch (Exception ex)
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
                var days = (leave.ToDate - leave.FromDate).Days + 1;
                var dateRange = leave.FromDate.ToString("MMMM dd, yyyy");
                if (leave.FromDate.Date != leave.ToDate.Date)
                    dateRange = $"{leave.FromDate:MMMM dd, yyyy} - {leave.ToDate:MMMM dd, yyyy}";

                leave.Status = "Cancelled";
                await _db.SaveChangesAsync();

                // Notify admin about cancellation
                var admin = await _db.Users.FirstOrDefaultAsync(u => u.RoleId == 1);
                if (admin != null)
                {
                    try
                    {
                        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                        var userEmail = user?.Email ?? "No email on file";

                        // Get initials safely
                        string firstNameInitial = !string.IsNullOrEmpty(employee.FirstName) && employee.FirstName.Length > 0
                            ? employee.FirstName.Substring(0, 1)
                            : "E";
                        string lastNameInitial = !string.IsNullOrEmpty(employee.LastName) && employee.LastName.Length > 0
                            ? employee.LastName.Substring(0, 1)
                            : "M";

                        var cancellationEmail = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Leave Cancellation Notification</title>
</head>
<body style='margin:0; padding:0; font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); padding:30px 0;'>
    <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background: transparent; padding:30px 0;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' border='0' style='background-color:#ffffff; border-radius:12px; box-shadow:0 20px 40px rgba(0,0,0,0.15); overflow:hidden;'>
                    
                    <!-- Cancellation Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); padding: 35px 40px; position:relative;'>
                            <div style='position:absolute; top:0; right:0; opacity:0.1;'>
                                <svg width='120' height='120' viewBox='0 0 24 24' fill='none' xmlns='http://www.w3.org/2000/svg'>
                                    <path d='M12 2C6.47 2 2 6.47 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm5 13.59L15.59 17 12 13.41 8.41 17 7 15.59 10.59 12 7 8.41 8.41 7 12 10.59 15.59 7 17 8.41 13.41 12 17 15.59z' fill='white'/>
                                </svg>
                            </div>
                            <h1 style='color:#ffffff; margin:0; font-size:28px; font-weight:600; letter-spacing:-0.5px;'>Leave Request Cancelled</h1>
                            <p style='color:#e0e0e0; margin:8px 0 0 0; font-size:16px; opacity:0.9;'>Employee withdrew leave application</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            
                            <!-- Cancellation Badge -->
                            <table width='100%' style='margin-bottom:30px;'>
                                <tr>
                                    <td align='center'>
                                        <table style='background-color:#FFEBEE; border-radius:30px; padding:12px 30px;'>
                                            <tr>
                                                <td>
                                                    <span style='color:#DC3545; font-weight:600; font-size:15px; text-transform:uppercase; letter-spacing:1px;'>
                                                        🚫 CANCELLED BY EMPLOYEE
                                                    </span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Employee Info Card -->
                            <table width='100%' style='background: linear-gradient(145deg, #fff5f5 0%, #ffeaea 100%); border-radius:16px; margin-bottom:25px; border-left:4px solid #f5576c;'>
                                <tr>
                                    <td style='padding:25px;'>
                                        <div style='display:flex; align-items:center; margin-bottom:15px;'>
                                            <div style='width:50px; height:50px; background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); border-radius:50%; display:flex; align-items:center; justify-content:center; margin-right:15px;'>
                                                <span style='color:white; font-size:20px; font-weight:600;'>{firstNameInitial}{lastNameInitial}</span>
                                            </div>
                                            <div>
                                                <h3 style='margin:0; color:#2C3E50; font-size:18px; font-weight:600;'>{employee.FirstName} {employee.LastName}</h3>
                                                <p style='margin:5px 0 0 0; color:#7F8C8D; font-size:14px;'>{userEmail}</p>
                                            </div>
                                        </div>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Cancelled Leave Details -->
                            <table width='100%' style='background-color:#ffffff; border:1px solid #FFEBEE; border-radius:16px; margin-bottom:25px; box-shadow:0 4px 6px rgba(0,0,0,0.02);'>
                                <tr>
                                    <td style='padding:25px;'>
                                        <h2 style='margin:0 0 20px 0; font-size:16px; color:#DC3545; font-weight:600; text-transform:uppercase; letter-spacing:0.5px; border-bottom:2px solid #FFEBEE; padding-bottom:12px;'>
                                            📋 Cancelled Leave Details
                                        </h2>
                                        <table width='100%'>
                                            <tr>
                                                <td width='40%' style='padding:10px 0; color:#7F8C8D; font-size:14px;'>Original Period</td>
                                                <td style='padding:10px 0; color:#2C3E50; font-weight:500; font-size:15px;'>{dateRange}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0; color:#7F8C8D; font-size:14px;'>Duration</td>
                                                <td style='padding:10px 0;'>
                                                    <span style='background:#DC3545; color:#ffffff; padding:6px 16px; border-radius:30px; font-weight:500; font-size:14px;'>
                                                        {days} Day{(days > 1 ? "s" : "")}
                                                    </span>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0; color:#7F8C8D; font-size:14px;'>Original Reason</td>
                                                <td style='padding:10px 0; color:#2C3E50; font-size:15px; line-height:1.5; background-color:#F8F9FA; border-radius:8px; padding:12px;'>{leave.Reason}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding:10px 0; color:#7F8C8D; font-size:14px;'>Cancelled On</td>
                                                <td style='padding:10px 0; color:#2C3E50; font-size:14px;'>{DateTime.Now:MMMM dd, yyyy} at {DateTime.Now:h:mm tt}</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                            <!-- Action Note -->
                            <table width='100%'>
                                <tr>
                                    <td align='center'>
                                        <table style='background-color:#E3F2FD; border-radius:8px; padding:20px; width:100%;'>
                                            <tr>
                                                <td align='center' style='color:#1976D2; font-size:14px; line-height:1.6;'>
                                                    <span style='font-size:24px; display:block; margin-bottom:10px;'>📌</span>
                                                    <strong>No action required</strong><br>
                                                    This leave request has been cancelled by the employee and has been removed from the approval queue.
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background: linear-gradient(145deg, #2C3E50 0%, #1E2A3A 100%); padding:25px 40px;'>
                            <table width='100%'>
                                <tr>
                                    <td align='left' style='color:#BDC3C7; font-size:13px; line-height:1.6;'>
                                        <span style='font-weight:600; color:#ECF0F1; font-size:14px;'>SquadInternal HR System</span><br>
                                        <span style='opacity:0.8;'>Automatic notification - No reply needed</span><br>
                                        <span style='opacity:0.6;'>© {DateTime.Now.Year} All Rights Reserved</span>
                                    </td>
                                    <td align='right'>
                                        <span style='color:#95A5A6; font-size:12px;'>System Update</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

                        string subject = days > 1
                            ? $"🚫 Leave Cancelled: {employee.FirstName} {employee.LastName} - {days} Days"
                            : $"🚫 Leave Cancelled: {employee.FirstName} {employee.LastName} - {days} Day";

                        _emailService.SendEmail(admin.Email, subject, cancellationEmail);
                    }
                    catch (Exception)
                    {
                        // Log error but don't block user
                    }
                }

                TempData["Success"] = "Leave cancelled successfully.";
            }
            else
            {
                TempData["Error"] = "Only pending leaves can be cancelled.";
            }

            return RedirectToAction("Leave");
        }
    }
}