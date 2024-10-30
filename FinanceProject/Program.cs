using FinanceManager.Services;
using FinanceManager.Data;
using FinanceManager.Settings;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using DinkToPdf;
using DinkToPdf.Contracts;
using FinanceManager.Models;
using System.Runtime.InteropServices;
using FinanceManager;
using FinanceManager.FinanceManager;


var builder = WebApplication.CreateBuilder(args);

// Initialize DinkToPdf with custom loader
var assemblyLoadContext = new CustomAssemblyLoadContext();
var architectureFolder = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "x64" : "x86";
var libraryPath = Path.Combine(Directory.GetCurrentDirectory(), "Native", "libwkhtmltox.dll");
assemblyLoadContext.LoadUnmanagedLibrary(libraryPath);

// Set EPPlus license context
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// Add services to the container
builder.Services.AddControllersWithViews();

// Configure DinkToPdf
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Register Services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IReportService, ReportService>();

// Configure Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "FinanceManager.Cookie";
        options.Cookie.HttpOnly = true;
    });

// Add session support
builder.Services.AddSession();

// Configure ApplicationSettings
builder.Services.Configure<ApplicationSettings>(
    builder.Configuration.GetSection("ApplicationSettings"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        // Only create database if it doesn't exist
        await dbContext.Database.EnsureCreatedAsync();

        // Add default categories if they don't exist
        if (!dbContext.Categories.Any())
        {
            var defaultCategories = new List<Category>
            {
                new Category { Name = "Housing", Description = "Rent, Mortgage, Utilities", Type = CategoryType.Expense, ColorCode = "#FF5733", IsSystem = true },
                new Category { Name = "Food", Description = "Groceries and Dining", Type = CategoryType.Expense, ColorCode = "#33FF57", IsSystem = true },
                new Category { Name = "Transportation", Description = "Car, Gas, Public Transit", Type = CategoryType.Expense, ColorCode = "#3357FF", IsSystem = true },
                new Category { Name = "Income", Description = "Salary and Other Income", Type = CategoryType.Income, ColorCode = "#57FF33", IsSystem = true }
            };

            await dbContext.Categories.AddRangeAsync(defaultCategories);
            await dbContext.SaveChangesAsync();
        }

        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
        throw; // Re-throw to prevent the app from starting with an invalid database
    }
}

app.Run();