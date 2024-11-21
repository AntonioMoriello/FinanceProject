using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;
using FinanceManager.Root;

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
        private readonly ILogger<TransactionService> _logger;

        public TransactionService(ApplicationDbContext context, ILogger<TransactionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Transaction>> GetUserTransactionsAsync(
    int userId,
    DateTime? startDate = null,
    DateTime? endDate = null,
    int? categoryId = null,
    TransactionType? type = null)
        {
            try
            {
                var query = _context.Transactions
                    .Include(t => t.Category)
                    .Include(t => t.User)
                    .Where(t => t.UserId == userId);

                if (startDate.HasValue)
                    query = query.Where(t => t.Date >= startDate.Value.StartOfDay());

                if (endDate.HasValue)
                    query = query.Where(t => t.Date <= endDate.Value.EndOfDay());

                if (categoryId.HasValue)
                    query = query.Where(t => t.CategoryId == categoryId.Value);

                if (type.HasValue)
                    query = query.Where(t => t.Type == type.Value);

                return await query
                    .OrderByDescending(t => t.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user transactions for userId: {UserId}", userId);
                return new List<Transaction>();
            }
        }

        public async Task<Transaction> GetTransactionByIdAsync(int transactionId, int userId)
        {
            return await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.UserId == userId);
        }

        public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
        {
            try
            {
                // Validate required fields
                if (transaction == null)
                    throw new ArgumentNullException(nameof(transaction));

                if (transaction.UserId <= 0)
                    throw new ArgumentException("Invalid UserId");

                if (transaction.CategoryId <= 0)
                    throw new ArgumentException("Invalid CategoryId");

                if (transaction.Amount <= 0)
                    throw new ArgumentException("Amount must be greater than zero");

                // Verify category exists and belongs to user or is system
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == transaction.CategoryId &&
                        (c.UserId == transaction.UserId || c.IsSystem));

                if (category == null)
                    throw new InvalidOperationException($"Category with ID {transaction.CategoryId} not found or not accessible");

                // Verify user exists
                var userExists = await _context.Users
                    .AnyAsync(u => u.UserId == transaction.UserId);

                if (!userExists)
                    throw new InvalidOperationException($"User with ID {transaction.UserId} not found");

                // Set up recurring transaction if needed
                if (transaction.IsRecurring)
                {
                    if (string.IsNullOrEmpty(transaction.RecurrencePattern))
                        throw new InvalidOperationException("Recurrence pattern is required for recurring transactions");

                    transaction.NextRecurrenceDate = CalculateNextRecurrenceDate(
                        transaction.Date,
                        transaction.RecurrencePattern);
                }

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                return transaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction for userId: {UserId}", transaction?.UserId);
                throw;
            }
        }

        private bool IsValidRecurrencePattern(string pattern)
        {
            var validPatterns = new[] { "daily", "weekly", "monthly", "yearly" };
            return validPatterns.Contains(pattern.ToLower());
        }

        public async Task<Transaction> UpdateTransactionAsync(Transaction transaction)
        {
            try
            {
                var existingTransaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == transaction.TransactionId &&
                        t.UserId == transaction.UserId);

                if (existingTransaction == null)
                    throw new InvalidOperationException("Transaction not found or unauthorized");

                // Update only allowed fields
                existingTransaction.Amount = transaction.Amount;
                existingTransaction.Description = transaction.Description;
                existingTransaction.Date = transaction.Date;
                existingTransaction.CategoryId = transaction.CategoryId;
                existingTransaction.Type = transaction.Type;
                existingTransaction.IsRecurring = transaction.IsRecurring;
                existingTransaction.RecurrencePattern = transaction.RecurrencePattern;
                existingTransaction.NextRecurrenceDate = transaction.NextRecurrenceDate;

                await _context.SaveChangesAsync();
                return existingTransaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction: {TransactionId}", transaction.TransactionId);
                throw;
            }
        }

        public async Task DeleteTransactionAsync(int transactionId, int userId)
        {
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var transactionToDelete = await _context.Transactions
                            .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.UserId == userId);

                        if (transactionToDelete != null)
                        {
                            _context.Transactions.Remove(transactionToDelete);
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            _logger.LogInformation("Transaction {TransactionId} deleted successfully", transactionId);
                        }
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction: {TransactionId}", transactionId);
                throw;
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

            // Execute query and perform aggregation in memory
            var transactions = await query.ToListAsync();

            return transactions
                .GroupBy(t => t.Category.Name)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(t => t.Amount)
                );
        }

        public async Task<(decimal income, decimal expenses)> GetTotalsByTypeAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Transactions.Where(t => t.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value);

            // Execute query and perform aggregation in memory
            var transactions = await query.ToListAsync();

            var income = transactions
                .Where(t => t.Type == TransactionType.Income)
                .Sum(t => t.Amount);

            var expenses = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => t.Amount);

            return (income, expenses);
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
            if (string.IsNullOrEmpty(pattern))
                return null;

            return pattern.ToLower() switch
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