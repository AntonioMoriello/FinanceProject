using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Models;
using FinanceManager.Models.ViewModels;
using FinanceManager.Services;
using FinanceManager.Data;
using System.Security.Claims;

namespace FinanceManager.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly ITransactionService _transactionService;
        private readonly ApplicationDbContext _context;

        public TransactionsController(ITransactionService transactionService, ApplicationDbContext context)
        {
            _transactionService = transactionService;
            _context = context;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        // GET: Transactions
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, int? categoryId, TransactionType? type)
        {
            var userId = GetUserId();
            var transactions = await _transactionService.GetUserTransactionsAsync(userId, startDate, endDate, categoryId, type);
            var (income, expenses) = await _transactionService.GetTotalsByTypeAsync(userId, startDate, endDate);
            var categoryTotals = await _transactionService.GetCategoryTotalsAsync(userId, startDate, endDate);

            var viewModel = new TransactionListViewModel
            {
                Transactions = transactions,
                TotalIncome = income,
                TotalExpenses = expenses,
                NetAmount = income - expenses,
                CategoryTotals = categoryTotals,
                StartDate = startDate,
                EndDate = endDate,
                CategoryId = categoryId,
                Type = type,
                Categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync()
            };

            return View(viewModel);
        }

        // GET: Transactions/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetUserId();
            var transaction = await _transactionService.GetTransactionByIdAsync(id, userId);

            if (transaction == null)
                return NotFound();

            return View(transaction);
        }

        // GET: Transactions/Create
        public async Task<IActionResult> Create()
        {
            var userId = GetUserId();
            var viewModel = new TransactionCreateViewModel
            {
                Date = DateTime.Today,
                Categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync()
            };

            return View(viewModel);
        }

        // POST: Transactions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TransactionCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var transaction = new Transaction
                {
                    UserId = GetUserId(),
                    CategoryId = viewModel.CategoryId,
                    Amount = viewModel.Amount,
                    Description = viewModel.Description,
                    Date = viewModel.Date,
                    Type = viewModel.Type,
                    IsRecurring = viewModel.IsRecurring,
                    RecurrencePattern = viewModel.RecurrencePattern,
                    NextRecurrenceDate = viewModel.IsRecurring ? viewModel.Date.AddDays(1) : null
                };

                await _transactionService.CreateTransactionAsync(transaction);
                return RedirectToAction(nameof(Index));
            }

            viewModel.Categories = await _context.Categories.Where(c => c.UserId == GetUserId()).ToListAsync();
            return View(viewModel);
        }

        // GET: Transactions/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetUserId();
            var transaction = await _transactionService.GetTransactionByIdAsync(id, userId);

            if (transaction == null)
                return NotFound();

            var viewModel = new TransactionEditViewModel
            {
                TransactionId = transaction.TransactionId,
                Amount = transaction.Amount,
                CategoryId = transaction.CategoryId,
                Date = transaction.Date,
                Description = transaction.Description,
                Type = transaction.Type,
                IsRecurring = transaction.IsRecurring,
                RecurrencePattern = transaction.RecurrencePattern,
                Categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync()
            };

            return View(viewModel);
        }

        // POST: Transactions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TransactionEditViewModel viewModel)
        {
            if (id != viewModel.TransactionId)
                return NotFound();

            if (ModelState.IsValid)
            {
                var transaction = new Transaction
                {
                    TransactionId = viewModel.TransactionId,
                    UserId = GetUserId(),
                    CategoryId = viewModel.CategoryId,
                    Amount = viewModel.Amount,
                    Description = viewModel.Description,
                    Date = viewModel.Date,
                    Type = viewModel.Type,
                    IsRecurring = viewModel.IsRecurring,
                    RecurrencePattern = viewModel.RecurrencePattern,
                    NextRecurrenceDate = viewModel.IsRecurring ? CalculateNextRecurrenceDate(viewModel.Date, viewModel.RecurrencePattern) : null
                };

                await _transactionService.UpdateTransactionAsync(transaction);
                return RedirectToAction(nameof(Index));
            }

            viewModel.Categories = await _context.Categories.Where(c => c.UserId == GetUserId()).ToListAsync();
            return View(viewModel);
        }

        // GET: Transactions/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            var transaction = await _transactionService.GetTransactionByIdAsync(id, userId);

            if (transaction == null)
                return NotFound();

            return View(transaction);
        }

        // POST: Transactions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetUserId();
            await _transactionService.DeleteTransactionAsync(id, userId);
            return RedirectToAction(nameof(Index));
        }

        private DateTime? CalculateNextRecurrenceDate(DateTime currentDate, string pattern)
        {
            return pattern?.ToLower() switch
            {
                "daily" => currentDate.AddDays(1),
                "weekly" => currentDate.AddDays(7),
                "monthly" => currentDate.AddMonths(1),
                "yearly" => currentDate.AddYears(1),
                _ => null
            };
        }
    }
}