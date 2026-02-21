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
    }
}
