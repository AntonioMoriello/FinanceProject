using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;
using FinanceManager.Models.ViewModels;

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

        public GoalService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Goal>> GetUserGoalsAsync(int userId, GoalType? type = null, GoalStatus? status = null)
        {
            var query = _context.Goals.Where(g => g.UserId == userId);

            if (type.HasValue)
                query = query.Where(g => g.Type == type.Value);

            if (status.HasValue)
                query = query.Where(g => g.Status == status.Value);

            return await query.OrderByDescending(g => g.StartDate).ToListAsync();
        }

        public async Task<Goal> GetGoalByIdAsync(int goalId, int userId)
        {
            return await _context.Goals
                .FirstOrDefaultAsync(g => g.GoalId == goalId && g.UserId == userId);
        }

        public async Task<Goal> CreateGoalAsync(Goal goal)
        {
            _context.Goals.Add(goal);
            await _context.SaveChangesAsync();
            return goal;
        }

        public async Task<Goal> UpdateGoalAsync(Goal goal)
        {
            _context.Entry(goal).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return goal;
        }

        public async Task DeleteGoalAsync(int goalId, int userId)
        {
            var goal = await _context.Goals
                .FirstOrDefaultAsync(g => g.GoalId == goalId && g.UserId == userId);

            if (goal != null)
            {
                _context.Goals.Remove(goal);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Dictionary<int, decimal>> GetProgressPercentagesAsync(IEnumerable<Goal> goals)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var goal in goals)
            {
                result[goal.GoalId] = goal.TargetAmount > 0
                    ? (goal.CurrentAmount / goal.TargetAmount) * 100
                    : 0;
            }

            return result;
        }

        public async Task<Dictionary<int, decimal>> GetRemainingAmountsAsync(IEnumerable<Goal> goals)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var goal in goals)
            {
                result[goal.GoalId] = goal.TargetAmount - goal.CurrentAmount;
            }

            return result;
        }

        public async Task<Dictionary<int, int>> GetRemainingDaysAsync(IEnumerable<Goal> goals)
        {
            var result = new Dictionary<int, int>();

            foreach (var goal in goals)
            {
                result[goal.GoalId] = Math.Max(0, (goal.TargetDate - DateTime.Today).Days);
            }

            return result;
        }

        public async Task<decimal> UpdateProgressAsync(int goalId, int userId, decimal contributionAmount)
        {
            var goal = await GetGoalByIdAsync(goalId, userId);
            if (goal == null) return 0;

            goal.CurrentAmount += contributionAmount;

            // Record the contribution in transaction history
            var transaction = new Transaction
            {
                UserId = userId,
                Amount = contributionAmount,
                Date = DateTime.Now,
                Description = $"Contribution to goal: {goal.Name}",
                Type = TransactionType.Expense
            };

            _context.Transactions.Add(transaction);

            if (goal.CurrentAmount >= goal.TargetAmount)
            {
                goal.Status = GoalStatus.Completed;
            }

            await _context.SaveChangesAsync();
            return goal.CurrentAmount;
        }

        public async Task<IEnumerable<ContributionHistory>> GetContributionHistoryAsync(int goalId, int userId)
        {
            var goal = await GetGoalByIdAsync(goalId, userId);
            if (goal == null) return new List<ContributionHistory>();

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

        public async Task<decimal> CalculateRequiredMonthlyContributionAsync(Goal goal)
        {
            var remainingAmount = goal.TargetAmount - goal.CurrentAmount;
            var remainingMonths = Math.Max(1, (goal.TargetDate - DateTime.Today).Days / 30.0m);

            return Math.Ceiling(remainingAmount / remainingMonths);
        }
    }
}