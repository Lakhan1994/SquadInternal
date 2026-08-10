# Copilot Instructions

## Project Guidelines
- In the SquadInternal project, admin-only controller actions should be secured with the existing [AuthorizeAdmin] attribute (a subclass of SessionAuthorizeAttribute), and modal-based UI patterns (Bootstrap modal + AJAX-loaded partial views) are the preferred style for admin CRUD features in Views/Admin/*.cshtml.