using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;
using FinanceManager.Models.ViewModels;
using FinanceManager.Root;

namespace FinanceManager.Services
{
    public interface IGoalService
    {
        Task<IEnumerable<Goal>> GetUserGoalsAsync(int userId, GoalType? type = null, GoalStatus? status = null);
        Task<Goal> GetGoalByIdAsync(int goalId, int userId);
        Task<Goal> CreateGoalAsync(Goal goal);
        Task<Goal> UpdateGoalAsync(Goal goal);
        Task DeleteGoalAsync(int goalId, int userId);
        Task<Dictionary<int, decimal>> GetProgressPercentagesAsync(IEnumerable<Goal> goals);
        Task<Dictionary<int, decimal>> GetRemainingAmountsAsync(IEnumerable<Goal> goals);
        Task<Dictionary<int, int>> GetRemainingDaysAsync(IEnumerable<Goal> goals);
        Task<decimal> UpdateProgressAsync(int goalId, int userId, decimal contributionAmount);
        Task<IEnumerable<ContributionHistory>> GetContributionHistoryAsync(int goalId, int userId);
        Task<decimal> CalculateRequiredMonthlyContributionAsync(Goal goal);
    }

    public class GoalService : IGoalService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GoalService> _logger;

        public GoalService(ApplicationDbContext context, ILogger<GoalService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Goal>> GetUserGoalsAsync(int userId, GoalType? type = null, GoalStatus? status = null)
        {
            try
            {
                var query = _context.Goals.Where(g => g.UserId == userId);

                if (type.HasValue)
                    query = query.Where(g => g.Type == type.Value);

                if (status.HasValue)
                    query = query.Where(g => g.Status == status.Value);

                return await query.OrderByDescending(g => g.StartDate).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user goals for userId: {UserId}", userId);
                return new List<Goal>();
            }
        }

        public async Task<Goal> GetGoalByIdAsync(int goalId, int userId)
        {
            try
            {
                return await _context.Goals
                    .FirstOrDefaultAsync(g => g.GoalId == goalId && g.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting goal by id: {GoalId}", goalId);
                return null;
            }
        }

        public async Task<Goal> CreateGoalAsync(Goal goal)
        {
            try
            {
                if (goal == null)
                    throw new ArgumentNullException(nameof(goal));

                _context.Goals.Add(goal);
                await _context.SaveChangesAsync();
                return goal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating goal for userId: {UserId}", goal?.UserId);
                throw;
            }
        }

        public async Task<Goal> UpdateGoalAsync(Goal goal)
        {
            try
            {
                _context.Entry(goal).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return goal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating goal: {GoalId}", goal?.GoalId);
                throw;
            }
        }

        public async Task DeleteGoalAsync(int goalId, int userId)
        {
            try
            {
                // Create an execution strategy
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var goal = await _context.Goals
                            .Include(g => g.User)
                            .FirstOrDefaultAsync(g => g.GoalId == goalId && g.UserId == userId);

                        if (goal != null)
                        {
                            // Get all transactions related to this goal
                            var relatedTransactions = await _context.Transactions
                                .Where(t => t.Description.Contains($"Contribution to goal: {goal.Name}"))
                                .ToListAsync();

                            // Remove related transactions first
                            if (relatedTransactions.Any())
                            {
                                _context.Transactions.RemoveRange(relatedTransactions);
                            }

                            // Then remove the goal
                            _context.Goals.Remove(goal);

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            _logger.LogInformation("Goal {GoalId} and its related data deleted successfully", goalId);
                        }
                        else
                        {
                            _logger.LogWarning("Attempt to delete non-existent goal: {GoalId}", goalId);
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
                _logger.LogError(ex, "Error deleting goal: {GoalId}", goalId);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetProgressPercentagesAsync(IEnumerable<Goal> goals)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var goal in goals)
            {
                try
                {
                    result[goal.GoalId] = goal.TargetAmount > 0
                        ? Math.Round((goal.CurrentAmount / goal.TargetAmount) * 100, 2)
                        : 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating progress percentage for goal: {GoalId}", goal.GoalId);
                    result[goal.GoalId] = 0;
                }
            }

            return result;
        }

        public async Task<Dictionary<int, decimal>> GetRemainingAmountsAsync(IEnumerable<Goal> goals)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var goal in goals)
            {
                try
                {
                    result[goal.GoalId] = goal.TargetAmount - goal.CurrentAmount;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating remaining amount for goal: {GoalId}", goal.GoalId);
                    result[goal.GoalId] = goal.TargetAmount; // Default to full amount if calculation fails
                }
            }

            return result;
        }

        public async Task<Dictionary<int, int>> GetRemainingDaysAsync(IEnumerable<Goal> goals)
        {
            var result = new Dictionary<int, int>();
            var today = DateTime.Today.StartOfDay();

            foreach (var goal in goals)
            {
                try
                {
                    var targetDate = goal.TargetDate.EndOfDay();
                    result[goal.GoalId] = Math.Max(0, (targetDate - today).Days);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating remaining days for goal: {GoalId}", goal.GoalId);
                    result[goal.GoalId] = 0;
                }
            }

            return result;
        }

        public async Task<decimal> UpdateProgressAsync(int goalId, int userId, decimal contributionAmount)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var goal = await _context.Goals
                    .FirstOrDefaultAsync(g => g.GoalId == goalId && g.UserId == userId);

                if (goal == null)
                    throw new InvalidOperationException("Goal not found or unauthorized");

                goal.CurrentAmount += contributionAmount;

                // Record the contribution
                var contribution = new Transaction
                {
                    UserId = userId,
                    Amount = contributionAmount,
                    Date = DateTime.UtcNow,
                    Description = $"Contribution to goal: {goal.Name}",
                    Type = TransactionType.Expense,
                    CategoryId = 1 // Assuming 1 is a valid category ID for savings/investments
                };

                _context.Transactions.Add(contribution);

                // Update goal status if target reached
                if (goal.CurrentAmount >= goal.TargetAmount)
                {
                    goal.Status = GoalStatus.Completed;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return goal.CurrentAmount;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating goal progress: {GoalId}", goalId);
                throw;
            }
        }

        public async Task<IEnumerable<ContributionHistory>> GetContributionHistoryAsync(int goalId, int userId)
        {
            try
            {
                var goal = await GetGoalByIdAsync(goalId, userId);
                if (goal == null)
                    return new List<ContributionHistory>();

                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId &&
                               t.Description.Contains($"Contribution to goal: {goal.Name}"))
                    .OrderByDescending(t => t.Date)
                    .Take(10)
                    .Select(t => new ContributionHistory
                    {
                        Date = t.Date,
                        Amount = t.Amount,
                        Note = t.Description
                    })
                    .ToListAsync();

                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contribution history for goal: {GoalId}", goalId);
                return new List<ContributionHistory>();
            }
        }

        public async Task<decimal> CalculateRequiredMonthlyContributionAsync(Goal goal)
        {
            try
            {
                if (goal == null)
                    throw new ArgumentNullException(nameof(goal));

                var today = DateTime.Today.StartOfDay();
                var targetDate = goal.TargetDate.EndOfDay();
                var remainingAmount = goal.TargetAmount - goal.CurrentAmount;
                var remainingMonths = Math.Max(1, (targetDate - today).Days / 30.0m);

                return Math.Ceiling(remainingAmount / remainingMonths);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating required monthly contribution for goal: {GoalId}", goal?.GoalId);
                throw;
            }
        }
    }
}