using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Services;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;

namespace SquadInternal.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PasswordService _passwordService;

        public AccountController(AppDbContext db, PasswordService passwordService)
        {
            _db = db;
            _passwordService = passwordService;
        }

        // -------------------- LOGIN (GET) --------------------
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // -------------------- LOGIN (POST) --------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Email and password are required.";
                return View();
            }

            email = email.Trim().ToLower();

            // Case-insensitive email check
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.Email != null &&
                    u.Email.ToLower() == email);

            if (user == null)
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            if (!user.IsActive)
            {
                ViewBag.Error = "Your account is disabled. Contact admin.";
                return View();
            }

            if (!_passwordService.Verify(user, password))
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            // Clear old session
            HttpContext.Session.Clear();

            // Set new session
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetInt32("RoleId", user.RoleId);
            HttpContext.Session.SetString("UserName", user.Name ?? "User");

            // Redirect (same for all roles currently)
            return RedirectToAction("Dashboard", "Admin");
        }

        // -------------------- LOGOUT --------------------
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId);

            if (user == null)
                return RedirectToAction("Login");

            // Verify current password
            bool isValid = _passwordService.Verify(user, currentPassword);

            if (!isValid)
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction("Dashboard", "Admin");
            }

            // Update password
            user.PasswordHash = _passwordService.HashPassword(newPassword);

            await _db.SaveChangesAsync();

            TempData["Success"] = "Password updated successfully.";

            return RedirectToAction("Dashboard", "Admin");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> SendOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { success = false, message = "Email is required." });

            email = email.Trim().ToLower();

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);

            if (user == null)
                return Json(new { success = false, message = "Email not found." });

            var otp = new Random().Next(100000, 999999).ToString();

            user.ResetOtp = otp;
            user.OtpExpiry = DateTime.Now.AddMinutes(5);

            await _db.SaveChangesAsync();

            await SendEmail(email, otp);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                return Json(new { success = false, message = "Invalid or expired OTP." });

            email = email.Trim().ToLower();
            otp = otp.Trim();

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);

            if (user == null)
                return Json(new { success = false, message = "Invalid or expired OTP." });

            if (user.ResetOtp != otp)
                return Json(new { success = false, message = "Invalid or expired OTP." });

            if (user.OtpExpiry == null || user.OtpExpiry < DateTime.Now)
                return Json(new { success = false, message = "Invalid or expired OTP." });

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
                return Json(new { success = false, message = "Passwords do not match." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return Json(new { success = false, message = "User not found." });

            user.PasswordHash = _passwordService.HashPassword(newPassword);
            user.ResetOtp = null;
            user.OtpExpiry = null;

            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }
       
        private async Task SendEmail(string toEmail, string otp)
        {
            var fromEmail = "lavinathorat864@gmail.com";
            var fromPassword = "xyfvxewrnjeipsts";

            using (var smtp = new SmtpClient("smtp.gmail.com", 587))
            {
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(fromEmail, fromPassword);
                smtp.EnableSsl = true;

                using (var mail = new MailMessage(fromEmail, toEmail))
                {
                    mail.Subject = "SquadInternal | Password Reset OTP";

                    mail.IsBodyHtml = true;

                    mail.Body = $@"
            <div style='font-family: Arial, sans-serif; background-color:#f4f6f9; padding:20px;'>
                <div style='max-width:500px; margin:0 auto; background:white; padding:30px; border-radius:8px; box-shadow:0 2px 8px rgba(0,0,0,0.05);'>
                    
                    <h2 style='color:#0d6efd; margin-bottom:10px;'>SquadInternal</h2>
                    
                    <p style='font-size:14px; color:#333;'>
                        Hello,
                    </p>

                    <p style='font-size:14px; color:#333;'>
                        We Received a Request to Reset Your Password.
                    </p>

                    <p style='font-size:14px; color:#333;'>
                        Please Use the Following One-Time Password (OTP) to Proceed Your Request:
                    </p>

                    <div style='font-size:30px; font-weight:bold; 
                                letter-spacing:4px; 
                                text-align:center; 
                                margin:20px 0; 
                                color:#0d6efd;'>
                        {otp}
                    </div>

                    <p style='font-size:13px; color:#666;'>
                        This OTP is valid for 5 minutes. 
                        Do not share this code with anyone.
                    </p>

                    <hr style='margin:20px 0; border:none; border-top:1px solid #eee;' />

                    <p style='font-size:12px; color:#999;'>
                        If you did not request a password reset, please ignore this email.
                    </p>

                    <p style='font-size:12px; color:#999; margin-top:20px;'>
                        © {DateTime.Now.Year} SquadInternal. All rights reserved.
                    </p>

                </div>
            </div>";

                    await smtp.SendMailAsync(mail);
                }
            }
        }
    }
        }
    


            
        
