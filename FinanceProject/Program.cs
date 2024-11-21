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
using Microsoft.EntityFrameworkCore.SqlServer; // Add this line
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading.Tasks;

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
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });

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
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

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
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Create execution strategy
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            if (!await context.Database.CanConnectAsync())
            {
                logger.LogWarning("Unable to connect to database. Attempting to create database...");
                await context.Database.EnsureCreatedAsync();
            }

            // Check if we need to initialize default data
            if (!await context.Categories.AnyAsync())
            {
                logger.LogInformation("Initializing database with default data...");
                await DbInitializer.Initialize(context);
                logger.LogInformation("Database initialized successfully");
            }
            else
            {
                logger.LogInformation("Database already contains data. Skipping initialization.");
            }
        });
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while initializing the database.");
    throw;
}

app.Run();