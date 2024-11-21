using Microsoft.AspNetCore.Mvc;
using FinanceManager.Models.ViewModels;
using System.Diagnostics;
using FinanceManager.Services;
using Microsoft.AspNetCore.Authorization;
using FinanceManager.Models;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ITransactionService _transactionService;
        private readonly IBudgetService _budgetService;
        private readonly IGoalService _goalService;

        public HomeController(
            ILogger<HomeController> logger,
            ITransactionService transactionService,
            IBudgetService budgetService,
            IGoalService goalService)
        {
            _logger = logger;
            _transactionService = transactionService;
            _budgetService = budgetService;
            _goalService = goalService;
        }

        public IActionResult Index()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return View("Landing");
            }

            // Get the current user's ID
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // If we get here, user is authenticated and we have their ID
            // Return the dashboard view
            return View();
        }

        [Route("/Privacy")]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            var errorViewModel = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = statusCode,
                ShowDetailedError = HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            };

            if (statusCode.HasValue)
            {
                switch (statusCode.Value)
                {
                    case 404:
                        return View("Error", ErrorViewModel.NotFoundError());
                    case 401:
                        return View("Error", ErrorViewModel.UnauthorizedError());
                    case 400:
                        return View("Error", ErrorViewModel.BadRequestError());
                    case 500:
                        return View("Error", ErrorViewModel.ServerError());
                    default:
                        _logger.LogError($"Unhandled error status code: {statusCode}");
                        break;
                }
            }

            return View(errorViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                    return Unauthorized();

                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);

                // Get recent transactions
                var recentTransactions = await _transactionService.GetUserTransactionsAsync(
                    userId,
                    DateTime.Now.AddMonths(-1),
                    DateTime.Now);

                // Get active budgets
                var budgets = await _budgetService.GetUserBudgetsAsync(
                    userId,
                    DateTime.Now.StartOfMonth(),
                    DateTime.Now.EndOfMonth());

                // Get active goals
                var goals = await _goalService.GetUserGoalsAsync(
                    userId,
                    null,
                    GoalStatus.Active);

                // Calculate spending percentages for budgets
                var spendingPercentages = await _budgetService.GetSpendingPercentagesAsync(budgets);

                return Json(new
                {
                    recentTransactions = recentTransactions.Take(5).Select(t => new
                    {
                        date = t.Date,
                        description = t.Description,
                        amount = t.Amount,
                        category = t.Category.Name
                    }),
                    budgets = budgets.Select(b => new
                    {
                        name = b.Name,
                        limit = b.Amount,
                        spent = b.CurrentSpending ?? 0,
                        percentage = spendingPercentages[b.BudgetId]
                    }),
                    goals = goals.Select(g => new
                    {
                        name = g.Name,
                        target = g.TargetAmount,
                        current = g.CurrentAmount,
                        percentage = (g.CurrentAmount / g.TargetAmount) * 100
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard data");
                return StatusCode(500, "An error occurred while fetching dashboard data");
            }
        }
    }
}

public static class DateTimeExtensions
{
    public static DateTime StartOfMonth(this DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1);
    }

    public static DateTime EndOfMonth(this DateTime date)
    {
        return date.StartOfMonth().AddMonths(1).AddDays(-1);
    }
}