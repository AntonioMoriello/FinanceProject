using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;
using FinanceManager.Root;

namespace FinanceManager.Services
{
    public interface IBudgetService
    {
        Task<IEnumerable<Budget>> GetUserBudgetsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null, BudgetPeriod? period = null);
        Task<Budget> GetBudgetByIdAsync(int budgetId, int userId);
        Task<Budget> CreateBudgetAsync(Budget budget);
        Task<Budget> UpdateBudgetAsync(Budget budget);
        Task DeleteBudgetAsync(int budgetId, int userId);
        Task<decimal> CalculateCurrentSpendingAsync(int budgetId, int userId);
        Task<Dictionary<int, decimal>> GetSpendingPercentagesAsync(IEnumerable<Budget> budgets);
        Task<Dictionary<int, decimal>> GetRemainingAmountsAsync(IEnumerable<Budget> budgets);
        Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(int budgetId, int userId, int count = 5);
    }

    public class BudgetService : IBudgetService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BudgetService> _logger;

        public BudgetService(ApplicationDbContext context, ILogger<BudgetService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Budget>> GetUserBudgetsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null, BudgetPeriod? period = null)
        {
            try
            {
                var query = _context.Budgets
                    .Include(b => b.Category)
                    .Where(b => b.UserId == userId);

                if (startDate.HasValue)
                    query = query.Where(b => b.StartDate >= startDate.Value.StartOfDay());

                if (endDate.HasValue)
                    query = query.Where(b => b.EndDate <= endDate.Value.EndOfDay());

                if (period.HasValue)
                    query = query.Where(b => b.Period == period.Value);

                return await query.OrderByDescending(b => b.StartDate).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user budgets for userId: {UserId}", userId);
                return new List<Budget>();
            }
        }


        public async Task<Budget> GetBudgetByIdAsync(int budgetId, int userId)
        {
            try
            {
                return await _context.Budgets
                    .Include(b => b.Category)
                    .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting budget by id: {BudgetId}", budgetId);
                return null;
            }
        }

        public async Task<Budget> CreateBudgetAsync(Budget budget)
        {
            try
            {
                if (budget == null)
                    throw new ArgumentNullException(nameof(budget));

                // Validate category exists and belongs to user
                var categoryExists = await _context.Categories
                    .AnyAsync(c => c.CategoryId == budget.CategoryId &&
                        (c.UserId == budget.UserId || c.IsSystem));

                if (!categoryExists)
                    throw new InvalidOperationException("Invalid category");

                // Validate dates
                if (budget.StartDate >= budget.EndDate)
                    throw new InvalidOperationException("Start date must be before end date");

                _context.Budgets.Add(budget);
                await _context.SaveChangesAsync();

                return budget;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating budget for userId: {UserId}", budget?.UserId);
                throw;
            }
        }

        public async Task<Budget> UpdateBudgetAsync(Budget budget)
        {
            try
            {
                _context.Entry(budget).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return budget;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating budget: {BudgetId}", budget?.BudgetId);
                throw;
            }
        }

        public async Task DeleteBudgetAsync(int budgetId, int userId)
        {
            try
            {
                var budget = await _context.Budgets
                    .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);

                if (budget != null)
                {
                    _context.Budgets.Remove(budget);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting budget: {BudgetId}", budgetId);
                throw;
            }
        }

        public async Task<decimal> CalculateCurrentSpendingAsync(int budgetId, int userId)
        {
            try
            {
                var budget = await _context.Budgets
                    .Include(b => b.Category)
                    .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);

                if (budget == null)
                    return 0;

                return await _context.Transactions
                    .Where(t => t.UserId == userId &&
                               t.CategoryId == budget.CategoryId &&
                               t.Date >= budget.StartDate &&
                               t.Date <= budget.EndDate &&
                               t.Type == TransactionType.Expense)
                    .SumAsync(t => t.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating current spending for budget: {BudgetId}", budgetId);
                return 0;
            }
        }

        public async Task<Dictionary<int, decimal>> GetSpendingPercentagesAsync(IEnumerable<Budget> budgets)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var budget in budgets)
            {
                try
                {
                    var spending = await CalculateCurrentSpendingAsync(budget.BudgetId, budget.UserId);
                    result[budget.BudgetId] = budget.Amount > 0 ?
                        Math.Round((spending / budget.Amount) * 100, 2) : 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating spending percentage for budget: {BudgetId}",
                        budget.BudgetId);
                    result[budget.BudgetId] = 0;
                }
            }

            return result;
        }

        public async Task<Dictionary<int, decimal>> GetRemainingAmountsAsync(IEnumerable<Budget> budgets)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var budget in budgets)
            {
                try
                {
                    var spending = await CalculateCurrentSpendingAsync(budget.BudgetId, budget.UserId);
                    result[budget.BudgetId] = budget.Amount - spending;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating remaining amount for budget: {BudgetId}",
                        budget.BudgetId);
                    result[budget.BudgetId] = budget.Amount; // Default to full amount if calculation fails
                }
            }

            return result;
        }

        public async Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(int budgetId, int userId, int count = 5)
        {
            try
            {
                var budget = await _context.Budgets
                    .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);

                if (budget == null)
                    return new List<Transaction>();

                return await _context.Transactions
                    .Include(t => t.Category)
                    .Where(t => t.UserId == userId &&
                               t.CategoryId == budget.CategoryId &&
                               t.Date >= budget.StartDate &&
                               t.Date <= budget.EndDate)
                    .OrderByDescending(t => t.Date)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent transactions for budget: {BudgetId}", budgetId);
                return new List<Transaction>();
            }
        }
    }
}