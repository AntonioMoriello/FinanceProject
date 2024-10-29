using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinanceManager.Models;

namespace FinanceManager.Models.ViewModels
{
    public class GoalCreateViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Target Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TargetAmount { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Target Date")]
        [DataType(DataType.Date)]
        public DateTime TargetDate { get; set; } = DateTime.Today.AddYears(1);

        [Required]
        [Display(Name = "Goal Type")]
        public GoalType Type { get; set; }
    }

    public class GoalEditViewModel : GoalCreateViewModel
    {
        public int GoalId { get; set; }
        public decimal CurrentAmount { get; set; }
        public GoalStatus Status { get; set; }
    }

    public class GoalListViewModel
    {
        public IEnumerable<Goal> Goals { get; set; }
        public Dictionary<int, decimal> ProgressPercentages { get; set; }
        public Dictionary<int, decimal> RemainingAmounts { get; set; }
        public Dictionary<int, int> RemainingDays { get; set; }
        public GoalType? TypeFilter { get; set; }
        public GoalStatus? StatusFilter { get; set; }
    }

    public class GoalDetailsViewModel
    {
        public Goal Goal { get; set; }
        public decimal ProgressPercentage { get; set; }
        public decimal RemainingAmount { get; set; }
        public int RemainingDays { get; set; }
        public decimal RequiredMonthlyContribution { get; set; }
        public IEnumerable<ContributionHistory> ContributionHistory { get; set; }
    }

    public class ContributionHistory
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }
}