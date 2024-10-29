using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;

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

        public BudgetService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Budget>> GetUserBudgetsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null, BudgetPeriod? period = null)
        {
            var query = _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(b => b.StartDate >= startDate.Value || b.EndDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(b => b.StartDate <= endDate.Value || b.EndDate <= endDate.Value);

            if (period.HasValue)
                query = query.Where(b => b.Period == period.Value);

            return await query.OrderByDescending(b => b.StartDate).ToListAsync();
        }

        public async Task<Budget> GetBudgetByIdAsync(int budgetId, int userId)
        {
            return await _context.Budgets
                .Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);
        }

        public async Task<Budget> CreateBudgetAsync(Budget budget)
        {
            _context.Budgets.Add(budget);
            await _context.SaveChangesAsync();
            return budget;
        }

        public async Task<Budget> UpdateBudgetAsync(Budget budget)
        {
            _context.Entry(budget).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return budget;
        }

        public async Task DeleteBudgetAsync(int budgetId, int userId)
        {
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);

            if (budget != null)
            {
                _context.Budgets.Remove(budget);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<decimal> CalculateCurrentSpendingAsync(int budgetId, int userId)
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

        public async Task<Dictionary<int, decimal>> GetSpendingPercentagesAsync(IEnumerable<Budget> budgets)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var budget in budgets)
            {
                var spending = await CalculateCurrentSpendingAsync(budget.BudgetId, budget.UserId);
                result[budget.BudgetId] = budget.Amount > 0 ? (spending / budget.Amount) * 100 : 0;
            }

            return result;
        }

        public async Task<Dictionary<int, decimal>> GetRemainingAmountsAsync(IEnumerable<Budget> budgets)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var budget in budgets)
            {
                var spending = await CalculateCurrentSpendingAsync(budget.BudgetId, budget.UserId);
                result[budget.BudgetId] = budget.Amount - spending;
            }

            return result;
        }

        public async Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(int budgetId, int userId, int count = 5)
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
    }
}