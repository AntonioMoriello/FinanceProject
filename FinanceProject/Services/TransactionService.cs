using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public interface ITransactionService
    {
        Task<IEnumerable<Transaction>> GetUserTransactionsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null, int? categoryId = null, TransactionType? type = null);
        Task<Transaction> GetTransactionByIdAsync(int transactionId, int userId);
        Task<Transaction> CreateTransactionAsync(Transaction transaction);
        Task<Transaction> UpdateTransactionAsync(Transaction transaction);
        Task DeleteTransactionAsync(int transactionId, int userId);
        Task<Dictionary<string, decimal>> GetCategoryTotalsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<(decimal income, decimal expenses)> GetTotalsByTypeAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);
        Task ProcessRecurringTransactionsAsync();
    }

    public class TransactionService : ITransactionService
    {
        private readonly ApplicationDbContext _context;

        public TransactionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Transaction>> GetUserTransactionsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null, int? categoryId = null, TransactionType? type = null)
        {
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value);

            if (categoryId.HasValue)
                query = query.Where(t => t.CategoryId == categoryId.Value);

            if (type.HasValue)
                query = query.Where(t => t.Type == type.Value);

            return await query.OrderByDescending(t => t.Date).ToListAsync();
        }

        public async Task<Transaction> GetTransactionByIdAsync(int transactionId, int userId)
        {
            return await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.UserId == userId);
        }

        public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task<Transaction> UpdateTransactionAsync(Transaction transaction)
        {
            _context.Entry(transaction).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task DeleteTransactionAsync(int transactionId, int userId)
        {
            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.UserId == userId);

            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Dictionary<string, decimal>> GetCategoryTotalsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value);

            return await query
                .GroupBy(t => t.Category.Name)
                .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                .ToDictionaryAsync(x => x.Category, x => x.Total);
        }

        public async Task<(decimal income, decimal expenses)> GetTotalsByTypeAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Transactions.Where(t => t.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value);

            var totals = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
                .ToDictionaryAsync(x => x.Type, x => x.Total);

            return (
                totals.GetValueOrDefault(TransactionType.Income),
                totals.GetValueOrDefault(TransactionType.Expense)
            );
        }

        public async Task ProcessRecurringTransactionsAsync()
        {
            var recurringTransactions = await _context.Transactions
                .Where(t => t.IsRecurring && t.NextRecurrenceDate <= DateTime.Today)
                .ToListAsync();

            foreach (var transaction in recurringTransactions)
            {
                var newTransaction = new Transaction
                {
                    UserId = transaction.UserId,
                    CategoryId = transaction.CategoryId,
                    Amount = transaction.Amount,
                    Description = transaction.Description,
                    Type = transaction.Type,
                    Date = transaction.NextRecurrenceDate ?? DateTime.Today,
                    IsRecurring = true,
                    RecurrencePattern = transaction.RecurrencePattern,
                    NextRecurrenceDate = CalculateNextRecurrenceDate(transaction.NextRecurrenceDate ?? DateTime.Today, transaction.RecurrencePattern)
                };

                _context.Transactions.Add(newTransaction);

                // Update next recurrence date for the original transaction
                transaction.NextRecurrenceDate = CalculateNextRecurrenceDate(transaction.NextRecurrenceDate ?? DateTime.Today, transaction.RecurrencePattern);
            }

            await _context.SaveChangesAsync();
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