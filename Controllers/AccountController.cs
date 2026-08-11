using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Services;
using System.Threading.Tasks;

namespace SquadInternal.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PasswordService _passwordService;
        private readonly EmailService _emailService;

        public AccountController(AppDbContext db, PasswordService passwordService, EmailService emailService)
        {
            _db = db;
            _passwordService = passwordService;
            _emailService = emailService;
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

        // -------------------- CHANGE PASSWORD --------------------
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

        // -------------------- FORGOT PASSWORD (GET) --------------------
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // -------------------- SEND OTP --------------------
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

            // Send OTP using EmailService
            try
            {
                var emailBody = _emailService.GetOtpTemplate(user.Name ?? "User", otp);
                _emailService.SendEmail(email, "Password Reset OTP - SquadInternal", emailBody);

                return Json(new { success = true, message = "OTP sent successfully." });
            }
            catch (System.Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error sending email: {ex.Message}");
                return Json(new { success = false, message = "Failed to send OTP. Please try again." });
            }
        }

        // -------------------- VERIFY OTP --------------------
        [HttpPost]
        public async Task<IActionResult> VerifyOtp(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                return Json(new { success = false, message = "Email and OTP are required." });

            email = email.Trim().ToLower();
            otp = otp.Trim();

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);

            if (user == null)
                return Json(new { success = false, message = "Invalid email." });

            if (user.ResetOtp != otp)
                return Json(new { success = false, message = "Invalid OTP. Please check and try again." });

            if (user.OtpExpiry == null || user.OtpExpiry < DateTime.Now)
                return Json(new { success = false, message = "OTP has expired. Please request a new one." });

            // Clear OTP after successful verification
            user.ResetOtp = null;
            user.OtpExpiry = null;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "OTP verified successfully." });
        }

        // -------------------- RESET PASSWORD --------------------
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                return Json(new { success = false, message = "All fields are required." });

            if (newPassword.Length < 6)
                return Json(new { success = false, message = "Password must be at least 6 characters long." });

            if (newPassword != confirmPassword)
                return Json(new { success = false, message = "Passwords do not match." });

            email = email.Trim().ToLower();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);

            if (user == null)
                return Json(new { success = false, message = "User not found." });

            user.PasswordHash = _passwordService.HashPassword(newPassword);
            user.ResetOtp = null;
            user.OtpExpiry = null;

            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Password reset successfully. Please login with your new password." });
        }

        // -------------------- SEND WELCOME EMAIL --------------------
        public async Task<IActionResult> SendWelcomeEmail(int userId)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return Json(new { success = false, message = "User not found." });

            try
            {
                var portalUrl = $"{Request.Scheme}://{Request.Host}/Account/ForgotPassword";
                var emailBody = _emailService.GetEmployeeWelcomeTemplate(
                    user.Name ?? "Employee",
                    user.Email ?? "",
                    user.Role?.Name ?? "Employee",
                    portalUrl
                );

                _emailService.SendEmail(user.Email, "Welcome to SquadInternal!", emailBody);

                return Json(new { success = true, message = "Welcome email sent successfully." });
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error sending welcome email: {ex.Message}");
                return Json(new { success = false, message = "Failed to send welcome email." });
            }
        }

        // -------------------- RESEND OTP --------------------
        [HttpPost]
        public async Task<IActionResult> ResendOtp(string email)
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

            try
            {
                var emailBody = _emailService.GetOtpTemplate(user.Name ?? "User", otp);
                _emailService.SendEmail(email, "Password Reset OTP - SquadInternal", emailBody);

                return Json(new { success = true, message = "OTP resent successfully." });
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error resending OTP: {ex.Message}");
                return Json(new { success = false, message = "Failed to resend OTP. Please try again." });
            }
        }
    }
}