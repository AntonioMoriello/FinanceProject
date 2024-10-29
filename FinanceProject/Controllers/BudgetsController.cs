using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Models;
using FinanceManager.Models.ViewModels;
using FinanceManager.Services;
using FinanceManager.Data;
using System.Security.Claims;

namespace FinanceManager.Controllers;

[Authorize]
public class BudgetsController : Controller
{
        private readonly IBudgetService _budgetService;
        private readonly ApplicationDbContext _context;

        public BudgetsController(IBudgetService budgetService, ApplicationDbContext context)
        {
            _budgetService = budgetService;
            _context = context;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        // GET: Budgets
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, BudgetPeriod? period)
        {
            var userId = GetUserId();
            var budgets = await _budgetService.GetUserBudgetsAsync(userId, startDate, endDate, period);

            var viewModel = new BudgetListViewModel
            {
                Budgets = budgets,
                SpendingPercentages = await _budgetService.GetSpendingPercentagesAsync(budgets),
                RemainingAmounts = await _budgetService.GetRemainingAmountsAsync(budgets),
                StartDate = startDate,
                EndDate = endDate,
                Period = period
            };

            return View(viewModel);
        }

        // GET: Budgets/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetUserId();
            var budget = await _budgetService.GetBudgetByIdAsync(id, userId);

            if (budget == null)
                return NotFound();

            var spendingPercentages = await _budgetService.GetSpendingPercentagesAsync(new[] { budget });
            var remainingAmounts = await _budgetService.GetRemainingAmountsAsync(new[] { budget });
            var recentTransactions = await _budgetService.GetRecentTransactionsAsync(id, userId);

            var viewModel = new BudgetDetailsViewModel
            {
                Budget = budget,
                SpendingPercentage = spendingPercentages[budget.BudgetId],
                RemainingAmount = remainingAmounts[budget.BudgetId],
                RecentTransactions = recentTransactions
            };

            return View(viewModel);
        }

        // GET: Budgets/Create
        public async Task<IActionResult> Create()
        {
            var userId = GetUserId();
            var viewModel = new BudgetCreateViewModel
            {
                Categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync()
            };

            return View(viewModel);
        }

        // POST: Budgets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BudgetCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var budget = new Budget
                {
                    UserId = GetUserId(),
                    Name = viewModel.Name,
                    CategoryId = viewModel.CategoryId,
                    Amount = viewModel.Amount,
                    StartDate = viewModel.StartDate,
                    EndDate = viewModel.EndDate,
                    Period = viewModel.Period
                };

                await _budgetService.CreateBudgetAsync(budget);
                return RedirectToAction(nameof(Index));
            }

            viewModel.Categories = await _context.Categories.Where(c => c.UserId == GetUserId()).ToListAsync();
            return View(viewModel);
        }

        // GET: Budgets/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetUserId();
            var budget = await _budgetService.GetBudgetByIdAsync(id, userId);

            if (budget == null)
                return NotFound();

            var currentSpending = await _budgetService.CalculateCurrentSpendingAsync(id, userId);

            var viewModel = new BudgetEditViewModel
            {
                BudgetId = budget.BudgetId,
                Name = budget.Name,
                CategoryId = budget.CategoryId,
                Amount = budget.Amount,
                StartDate = budget.StartDate,
                EndDate = budget.EndDate,
                Period = budget.Period,
                CurrentSpending = currentSpending,
                Categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync()
            };

            return View(viewModel);
        }

        // POST: Budgets/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BudgetEditViewModel viewModel)
        {
            if (id != viewModel.BudgetId)
                return NotFound();

            if (ModelState.IsValid)
            {
                var budget = new Budget
                {
                    BudgetId = viewModel.BudgetId,
                    UserId = GetUserId(),
                    Name = viewModel.Name,
                    CategoryId = viewModel.CategoryId,
                    Amount = viewModel.Amount,
                    StartDate = viewModel.StartDate,
                    EndDate = viewModel.EndDate,
                    Period = viewModel.Period
                };

                await _budgetService.UpdateBudgetAsync(budget);
                return RedirectToAction(nameof(Index));
            }

            viewModel.Categories = await _context.Categories.Where(c => c.UserId == GetUserId()).ToListAsync();
            return View(viewModel);
        }

        // GET: Budgets/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            var budget = await _budgetService.GetBudgetByIdAsync(id, userId);

            if (budget == null)
                return NotFound();

            return View(budget);
        }

        // POST: Budgets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetUserId();
            await _budgetService.DeleteBudgetAsync(id, userId);
            return RedirectToAction(nameof(Index));
        }

        // GET: Budgets/Progress/5
        public async Task<IActionResult> Progress(int id)
        {
            var userId = GetUserId();
            var budget = await _budgetService.GetBudgetByIdAsync(id, userId);

            if (budget == null)
                return NotFound();

            var currentSpending = await _budgetService.CalculateCurrentSpendingAsync(id, userId);
            var spendingPercentage = budget.Amount > 0 ? (currentSpending / budget.Amount) * 100 : 0;
            var remainingAmount = budget.Amount - currentSpending;

            var viewModel = new BudgetDetailsViewModel
            {
                Budget = budget,
                SpendingPercentage = spendingPercentage,
                RemainingAmount = remainingAmount,
                RecentTransactions = await _budgetService.GetRecentTransactionsAsync(id, userId)
            };

            return View(viewModel);
        }

        // POST: Budgets/UpdateProgress/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress(int id)
        {
            var userId = GetUserId();
            var budget = await _budgetService.GetBudgetByIdAsync(id, userId);

            if (budget == null)
                return NotFound();

            var currentSpending = await _budgetService.CalculateCurrentSpendingAsync(id, userId);
            budget.CurrentSpending = currentSpending;

            await _budgetService.UpdateBudgetAsync(budget);
            return RedirectToAction(nameof(Progress), new { id });
        }

        // API endpoint for chart data
        [HttpGet]
        public async Task<IActionResult> GetBudgetChartData(int id)
        {
            var userId = GetUserId();
            var budget = await _budgetService.GetBudgetByIdAsync(id, userId);

            if (budget == null)
                return NotFound();

            var currentSpending = await _budgetService.CalculateCurrentSpendingAsync(id, userId);
            var remainingAmount = Math.Max(0, budget.Amount - currentSpending);

            var chartData = new
            {
                labels = new[] { "Spent", "Remaining" },
                datasets = new[]
                {
                    new
                    {
                        data = new[] { currentSpending, remainingAmount },
                        backgroundColor = new[] { "#FF6384", "#36A2EB" }
                    }
                }
            };

            return Json(chartData);
        }
    }
