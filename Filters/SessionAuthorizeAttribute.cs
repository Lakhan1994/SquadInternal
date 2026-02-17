using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;

namespace SquadInternal.Filters
{
    public enum SessionAuthMode
    {
        AdminOnly,
        EmployeeOnly,
        AdminOrHrOnly,
        AnyLoggedInUser   
    }

    public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly SessionAuthMode _mode;

        public SessionAuthorizeAttribute(SessionAuthMode mode)
        {
            _mode = mode;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.Session;

            var userId = session.GetInt32("UserId");
            var roleId = session.GetInt32("RoleId");
            var userName = session.GetString("UserName");

            // 🔒 Not logged in → go to login
            if (userId == null || roleId == null || string.IsNullOrEmpty(userName))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // ✅ Any logged-in user allowed (no role restriction)
            if (_mode == SessionAuthMode.AnyLoggedInUser)
            {
                return;
            }

            // 🔒 Admin only
            if (_mode == SessionAuthMode.AdminOnly && roleId != 1)
            {
                context.Result = new RedirectToActionResult("Logout", "Account", null);
                return;
            }

            // 🔒 Employee only (non-admin)
            if (_mode == SessionAuthMode.EmployeeOnly && roleId == 1)
            {
                context.Result = new RedirectToActionResult("Logout", "Account", null);
                return;
            }

            // 🔒 Admin OR HR only (Admin=1, HR=3)
            if (_mode == SessionAuthMode.AdminOrHrOnly && (roleId != 1 && roleId != 3))
            {
                context.Result = new RedirectToActionResult("Logout", "Account", null);
                return;
            }
        }
    }

    public class AuthorizeAdminAttribute : SessionAuthorizeAttribute
    {
        public AuthorizeAdminAttribute() : base(SessionAuthMode.AdminOnly) { }
    }

    public class AuthorizeEmployeeAttribute : SessionAuthorizeAttribute
    {
        public AuthorizeEmployeeAttribute() : base(SessionAuthMode.EmployeeOnly) { }
    }

    public class AuthorizeAdminOrHRAttribute : SessionAuthorizeAttribute
    {
        public AuthorizeAdminOrHRAttribute() : base(SessionAuthMode.AdminOrHrOnly) { }
    }
}
