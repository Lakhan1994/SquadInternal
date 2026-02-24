using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SquadInternal.Data;
using SquadInternal.Services;

var builder = WebApplication.CreateBuilder(args);

// EPPlus License
ExcelPackage.License.SetNonCommercialPersonal("SquadInternal");

// ================= SERVICES =================

// MVC
builder.Services.AddControllersWithViews();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Password hashing service
builder.Services.AddScoped<PasswordService>();

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add Authentication (Required for proper middleware flow)
builder.Services.AddAuthentication();
builder.Services.AddScoped<EmailService>();
var app = builder.Build();

// ================= MIDDLEWARE =================

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANT: Order matters
app.UseSession();          // Session first
app.UseAuthentication();   // Then Authentication
app.UseAuthorization();    // Then Authorization

// Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");



app.Run();
