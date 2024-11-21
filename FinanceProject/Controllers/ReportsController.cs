using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Models;
using FinanceManager.Models.ViewModels;
using FinanceManager.Services;
using FinanceManager.Data;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ApplicationDbContext _context;

        public ReportsController(IReportService reportService, ApplicationDbContext context)
        {
            _reportService = reportService;
            _context = context;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        // GET: Reports
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            var categories = await _context.Categories
                .Where(c => c.UserId == userId || c.IsSystem)
                .ToListAsync();

            var filter = new ReportFilterViewModel
            {
                StartDate = DateTime.Today.AddMonths(-1),
                EndDate = DateTime.Today,
                Categories = categories
            };

            return View(filter);
        }

        // GET: Reports/FinancialSummary
        public async Task<IActionResult> FinancialSummary(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = GetUserId();
            var summary = await _reportService.GetFinancialSummaryAsync(userId, startDate, endDate);
            return PartialView("_FinancialSummary", summary);
        }

        // GET: Reports/CashFlow
        public async Task<IActionResult> CashFlow(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = GetUserId();
            var report = await _reportService.GetCashFlowReportAsync(userId, startDate, endDate);
            return PartialView("_CashFlow", report);
        }

        // GET: Reports/BudgetAnalysis
        public async Task<IActionResult> BudgetAnalysis(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = GetUserId();
            var report = await _reportService.GetBudgetReportAsync(userId, startDate, endDate);
            return PartialView("_BudgetAnalysis", report);
        }

        // GET: Reports/Download
        public async Task<IActionResult> Download(ReportType reportType, string format, DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = GetUserId();
            string fileName = $"Financial_Report_{DateTime.Now:yyyyMMdd}";
            byte[] fileContents;
            string contentType;

            switch (format.ToLower())
            {
                case "pdf":
                    fileContents = await _reportService.GenerateReportPdfAsync(reportType, userId, startDate, endDate);
                    contentType = "application/pdf";
                    fileName += ".pdf";
                    break;

                case "excel":
                    fileContents = await _reportService.GenerateReportExcelAsync(reportType, userId, startDate, endDate);
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    fileName += ".xlsx";
                    break;

                default:
                    return BadRequest("Invalid format specified");
            }

            return File(fileContents, contentType, fileName);
        }

        // GET: Reports/CategoryBreakdown
        public async Task<IActionResult> CategoryBreakdown(DateTime? startDate = null, DateTime? endDate = null, int? categoryId = null)
        {
            var userId = GetUserId();
            var categories = await _context.Categories
                .Where(c => c.UserId == userId || c.IsSystem)
                .ToListAsync();

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .Where(t => !startDate.HasValue || t.Date >= startDate)
                .Where(t => !endDate.HasValue || t.Date <= endDate)
                .Where(t => !categoryId.HasValue || t.CategoryId == categoryId)
                .ToListAsync();

            var categoryBreakdown = transactions
                .GroupBy(t => t.Category.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    Amount = g.Sum(t => t.Amount),
                    Count = g.Count(),
                    Transactions = g.OrderByDescending(t => t.Date).Take(5)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            return Json(new
            {
                labels = categoryBreakdown.Select(c => c.Category),
                amounts = categoryBreakdown.Select(c => c.Amount),
                counts = categoryBreakdown.Select(c => c.Count),
                topTransactions = categoryBreakdown.Select(c => c.Transactions.Select(t => new
                {
                    date = t.Date.ToShortDateString(),
                    description = t.Description,
                    amount = t.Amount
                }))
            });
        }

        // GET: Reports/MonthlyTrends
        public async Task<IActionResult> MonthlyTrends(int months = 12)
        {
            var userId = GetUserId();
            var startDate = DateTime.Today.AddMonths(-months);

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId && t.Date >= startDate)
                .OrderBy(t => t.Date)
                .ToListAsync();

            var monthlyData = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => new
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                    Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
                    Net = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount) -
                          g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
                })
                .OrderBy(x => x.Month)
                .ToList();

            return Json(new
            {
                labels = monthlyData.Select(m => m.Month),
                income = monthlyData.Select(m => m.Income),
                expenses = monthlyData.Select(m => m.Expenses),
                net = monthlyData.Select(m => m.Net)
            });
        }

        // GET: Reports/GoalProgress
        public async Task<IActionResult> GoalProgress()
        {
            var userId = GetUserId();
            var goals = await _context.Goals
                .Where(g => g.UserId == userId && g.Status == GoalStatus.Active)
                .OrderByDescending(g => g.CurrentAmount / g.TargetAmount)
                .Take(5)
                .ToListAsync();

            return Json(new
            {
                labels = goals.Select(g => g.Name),
                current = goals.Select(g => g.CurrentAmount),
                target = goals.Select(g => g.TargetAmount),
                progress = goals.Select(g => (g.CurrentAmount / g.TargetAmount) * 100)
            });
        }

        // GET: Reports/BudgetOverview
        public async Task<IActionResult> BudgetOverview(DateTime? startDate = null, DateTime? endDate = null)
        {
            var userId = GetUserId();
            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .Where(b => !startDate.HasValue || b.StartDate <= endDate)
                .Where(b => !endDate.HasValue || b.EndDate >= startDate)
                .ToListAsync();

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .Where(t => !startDate.HasValue || t.Date >= startDate)
                .Where(t => !endDate.HasValue || t.Date <= endDate)
                .ToListAsync();

            var budgetOverview = budgets.Select(b => new
            {
                Category = b.Category.Name,
                Budgeted = b.Amount,
                Spent = transactions
                    .Where(t => t.CategoryId == b.CategoryId)
                    .Sum(t => t.Amount),
                Period = b.Period.ToString()
            }).ToList();

            return Json(new
            {
                categories = budgetOverview.Select(b => b.Category),
                budgeted = budgetOverview.Select(b => b.Budgeted),
                spent = budgetOverview.Select(b => b.Spent),
                periods = budgetOverview.Select(b => b.Period)
            });
        }

        // Helper method to validate date range
        private bool ValidateDateRange(DateTime? startDate, DateTime? endDate)
        {
            if (!startDate.HasValue || !endDate.HasValue)
                return true;

            return startDate <= endDate && startDate >= DateTime.Today.AddYears(-10);
        }
    }
}