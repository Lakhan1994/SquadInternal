using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using QuestPDF;
using QuestPDF.Infrastructure;
using SquadInternal.Data;
using SquadInternal.Services;
var builder = WebApplication.CreateBuilder(args);

// EPPlus License
ExcelPackage.License.SetNonCommercialPersonal("SquadInternal");

// QuestPDF license configuration - set to Community for open-source use
// See https://www.questpdf.com/license/configuration.html
QuestPDF.Settings.License = LicenseType.Community;

// ================= SERVICES =================

// MVC
builder.Services.AddControllersWithViews();



builder.Services.AddSingleton(x =>
{
    var config = x.GetRequiredService<IConfiguration>();
    var connectionString = config["AzureStorage:ConnectionString"];

    return new BlobServiceClient(connectionString);
});
// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(180);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

// Password hashing service
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<AnnualLeavePolicyService>();
builder.Services.AddScoped<AnnualLeaveRolloverService>();

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
builder.Services.AddSingleton<AzureBlobStorageService>();

if (builder.Environment.IsProduction() && builder.Configuration.GetValue<bool>("AnnualLeave:AutomaticRolloverEnabled"))
    builder.Services.AddHostedService<AnnualLeaveRolloverWorker>();


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
