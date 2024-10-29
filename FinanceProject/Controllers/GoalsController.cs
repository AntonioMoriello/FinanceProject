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
    public class GoalsController : Controller
    {
        private readonly IGoalService _goalService;
        private readonly ApplicationDbContext _context;

        public GoalsController(IGoalService goalService, ApplicationDbContext context)
        {
            _goalService = goalService;
            _context = context;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        // GET: Goals
        public async Task<IActionResult> Index(GoalType? type = null, GoalStatus? status = null)
        {
            var userId = GetUserId();
            var goals = await _goalService.GetUserGoalsAsync(userId, type, status);

            var viewModel = new GoalListViewModel
            {
                Goals = goals,
                ProgressPercentages = await _goalService.GetProgressPercentagesAsync(goals),
                RemainingAmounts = await _goalService.GetRemainingAmountsAsync(goals),
                RemainingDays = await _goalService.GetRemainingDaysAsync(goals),
                TypeFilter = type,
                StatusFilter = status
            };

            return View(viewModel);
        }

        // GET: Goals/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetUserId();
            var goal = await _goalService.GetGoalByIdAsync(id, userId);

            if (goal == null)
                return NotFound();

            var progressPercentages = await _goalService.GetProgressPercentagesAsync(new[] { goal });
            var remainingAmounts = await _goalService.GetRemainingAmountsAsync(new[] { goal });
            var remainingDays = await _goalService.GetRemainingDaysAsync(new[] { goal });
            var monthlyContribution = await _goalService.CalculateRequiredMonthlyContributionAsync(goal);
            var contributionHistory = await _goalService.GetContributionHistoryAsync(id, userId);

            var viewModel = new GoalDetailsViewModel
            {
                Goal = goal,
                ProgressPercentage = progressPercentages[goal.GoalId],
                RemainingAmount = remainingAmounts[goal.GoalId],
                RemainingDays = remainingDays[goal.GoalId],
                RequiredMonthlyContribution = monthlyContribution,
                ContributionHistory = contributionHistory
            };

            return View(viewModel);
        }

        // GET: Goals/Create
        public IActionResult Create()
        {
            var viewModel = new GoalCreateViewModel
            {
                StartDate = DateTime.Today,
                TargetDate = DateTime.Today.AddYears(1)
            };
            return View(viewModel);
        }

        // POST: Goals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GoalCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var goal = new Goal
                {
                    UserId = GetUserId(),
                    Name = viewModel.Name,
                    Description = viewModel.Description,
                    TargetAmount = viewModel.TargetAmount,
                    CurrentAmount = 0,
                    StartDate = viewModel.StartDate,
                    TargetDate = viewModel.TargetDate,
                    Type = viewModel.Type,
                    Status = GoalStatus.Active
                };

                await _goalService.CreateGoalAsync(goal);
                TempData["SuccessMessage"] = "Goal created successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(viewModel);
        }

        // GET: Goals/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetUserId();
            var goal = await _goalService.GetGoalByIdAsync(id, userId);

            if (goal == null)
                return NotFound();

            var viewModel = new GoalEditViewModel
            {
                GoalId = goal.GoalId,
                Name = goal.Name,
                Description = goal.Description,
                TargetAmount = goal.TargetAmount,
                CurrentAmount = goal.CurrentAmount,
                StartDate = goal.StartDate,
                TargetDate = goal.TargetDate,
                Type = goal.Type,
                Status = goal.Status
            };

            return View(viewModel);
        }

        // POST: Goals/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GoalEditViewModel viewModel)
        {
            if (id != viewModel.GoalId)
                return NotFound();

            if (ModelState.IsValid)
            {
                var goal = new Goal
                {
                    GoalId = viewModel.GoalId,
                    UserId = GetUserId(),
                    Name = viewModel.Name,
                    Description = viewModel.Description,
                    TargetAmount = viewModel.TargetAmount,
                    CurrentAmount = viewModel.CurrentAmount,
                    StartDate = viewModel.StartDate,
                    TargetDate = viewModel.TargetDate,
                    Type = viewModel.Type,
                    Status = viewModel.Status
                };

                try
                {
                    await _goalService.UpdateGoalAsync(goal);
                    TempData["SuccessMessage"] = "Goal updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await GoalExists(viewModel.GoalId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(viewModel);
        }

        // GET: Goals/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            var goal = await _goalService.GetGoalByIdAsync(id, userId);

            if (goal == null)
                return NotFound();

            return View(goal);
        }

        // POST: Goals/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetUserId();
            await _goalService.DeleteGoalAsync(id, userId);
            TempData["SuccessMessage"] = "Goal deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Goals/UpdateProgress/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress(int id, decimal contributionAmount)
        {
            var userId = GetUserId();
            await _goalService.UpdateProgressAsync(id, userId, contributionAmount);
            TempData["SuccessMessage"] = "Progress updated successfully!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Goals/Progress/5
        public async Task<IActionResult> Progress(int id)
        {
            var userId = GetUserId();
            var goal = await _goalService.GetGoalByIdAsync(id, userId);

            if (goal == null)
                return NotFound();

            var progressPercentages = await _goalService.GetProgressPercentagesAsync(new[] { goal });
            var remainingAmounts = await _goalService.GetRemainingAmountsAsync(new[] { goal });
            var monthlyContribution = await _goalService.CalculateRequiredMonthlyContributionAsync(goal);
            var contributionHistory = await _goalService.GetContributionHistoryAsync(id, userId);

            var viewModel = new GoalDetailsViewModel
            {
                Goal = goal,
                ProgressPercentage = progressPercentages[goal.GoalId],
                RemainingAmount = remainingAmounts[goal.GoalId],
                RequiredMonthlyContribution = monthlyContribution,
                ContributionHistory = contributionHistory
            };

            return View(viewModel);
        }

        // API endpoint for chart data
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> GetGoalChartData(int id)
        {
            var userId = GetUserId();
            var goal = await _goalService.GetGoalByIdAsync(id, userId);

            if (goal == null)
                return NotFound();

            var contributionHistory = await _goalService.GetContributionHistoryAsync(id, userId);

            var chartData = new
            {
                labels = contributionHistory
                    .Select(h => ((Models.ViewModels.ContributionHistory)h).Date.ToShortDateString())
                    .ToList(),
                datasets = new[]
                {
                    new
                    {
                        label = "Contributions",
                        data = contributionHistory
                            .Select(h => ((Models.ViewModels.ContributionHistory)h).Amount)
                            .ToList(),
                        backgroundColor = "#4BC0C0",
                        borderColor = "#4BC0C0",
                        fill = false
                    }
                }
            };

            return Json(chartData);
        }

        private async Task<bool> GoalExists(int id)
        {
            var userId = GetUserId();
            var goal = await _goalService.GetGoalByIdAsync(id, userId);
            return goal != null;
        }
    }
}