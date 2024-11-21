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
    public class TransactionsController : Controller
    {
        private readonly ITransactionService _transactionService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TransactionsController> _logger;

        public TransactionsController(
            ITransactionService transactionService,
            ApplicationDbContext context,
            ILogger<TransactionsController> logger)
        {
            _transactionService = transactionService;
            _context = context;
            _logger = logger;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        // Move the CalculateNextRecurrenceDate method inside the class
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
                Categories = await _context.Categories
                    .Where(c => c.UserId == userId || c.IsSystem)  // Include both user's and system categories
                    .OrderBy(c => c.Name)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        // GET: Transactions/Create
        public async Task<IActionResult> Create()
        {
            var userId = GetUserId();
            var viewModel = new TransactionCreateViewModel
            {
                Date = DateTime.Today,
                Categories = await _context.Categories
                    .Where(c => c.UserId == userId || c.IsSystem)  // Include both user's and system categories
                    .OrderBy(c => c.Name)
                    .ToListAsync()
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
                try
                {
                    var userId = GetUserId();

                    // First verify the category exists and belongs to the user or is a system category
                    var category = await _context.Categories
                        .FirstOrDefaultAsync(c => c.CategoryId == viewModel.CategoryId &&
                            (c.UserId == userId || c.IsSystem));

                    if (category == null)
                    {
                        ModelState.AddModelError("CategoryId", "Invalid category selected.");
                        viewModel.Categories = await LoadCategoriesAsync(userId);
                        return View(viewModel);
                    }

                    var transaction = new Transaction
                    {
                        UserId = userId,
                        CategoryId = viewModel.CategoryId,
                        Amount = viewModel.Amount,
                        Description = viewModel.Description ?? string.Empty,
                        Date = viewModel.Date,
                        Type = viewModel.Type,
                        IsRecurring = viewModel.IsRecurring
                    };

                    // Only set recurring properties if IsRecurring is true
                    if (transaction.IsRecurring)
                    {
                        if (string.IsNullOrEmpty(viewModel.RecurrencePattern))
                        {
                            ModelState.AddModelError("RecurrencePattern", "A recurrence pattern is required for recurring transactions.");
                            viewModel.Categories = await LoadCategoriesAsync(userId);
                            return View(viewModel);
                        }

                        transaction.RecurrencePattern = viewModel.RecurrencePattern;
                        transaction.NextRecurrenceDate = CalculateNextRecurrenceDate(transaction.Date, transaction.RecurrencePattern);
                    }

                    _logger.LogInformation("Creating transaction: Category={CategoryId}, User={UserId}, IsRecurring={IsRecurring}",
                        transaction.CategoryId, transaction.UserId, transaction.IsRecurring);

                    await _transactionService.CreateTransactionAsync(transaction);
                    TempData["SuccessMessage"] = "Transaction created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating transaction");
                    ModelState.AddModelError("", $"Unable to save the transaction: {ex.Message}");
                }
            }

            // If we get here, something failed; redisplay form
            viewModel.Categories = await LoadCategoriesAsync(GetUserId());
            return View(viewModel);
        }

        private async Task<IEnumerable<Category>> LoadCategoriesAsync(int userId)
        {
            return await _context.Categories
                .Where(c => c.UserId == userId || c.IsSystem)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        // GET: Transactions/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var userId = GetUserId();
                var transaction = await _context.Transactions
                    .Include(t => t.Category)
                    .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

                if (transaction == null)
                    return NotFound();

                return View(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction details for ID: {TransactionId}", id);
                TempData["ErrorMessage"] = "Error retrieving transaction details.";
                return RedirectToAction(nameof(Index));
            }
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
                Categories = await _context.Categories
                    .Where(c => c.UserId == userId || c.IsSystem)  // Include both user's and system categories
                    .OrderBy(c => c.Name)
                    .ToListAsync()
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

            // If we get here, something failed; redisplay form with both user's and system categories
            var userId = GetUserId();
            viewModel.Categories = await _context.Categories
                .Where(c => c.UserId == userId || c.IsSystem)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(viewModel);
        }

        // GET: Transactions/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            var transaction = await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

            if (transaction == null)
                return NotFound();

            return View(transaction);
        }

        // POST: Transactions/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var userId = GetUserId();
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var transactionToDelete = await _context.Transactions
                            .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

                        if (transactionToDelete != null)
                        {
                            _context.Transactions.Remove(transactionToDelete);
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                        }
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                TempData["SuccessMessage"] = "Transaction deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction: {TransactionId}", id);
                TempData["ErrorMessage"] = "Error deleting transaction.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
   
           
        
    
