using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinanceManager.Models;

namespace FinanceManager.Models.ViewModels
{
    public class BudgetCreateViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);

        [Required]
        [Display(Name = "Budget Period")]
        public BudgetPeriod Period { get; set; }

        public IEnumerable<Category>? Categories { get; set; }
    }

    public class BudgetEditViewModel : BudgetCreateViewModel
    {
        public int BudgetId { get; set; }
        public decimal? CurrentSpending { get; set; }
    }

    public class BudgetListViewModel
    {
        public IEnumerable<Budget> Budgets { get; set; }
        public Dictionary<int, decimal> SpendingPercentages { get; set; }
        public Dictionary<int, decimal> RemainingAmounts { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public BudgetPeriod? Period { get; set; }
    }

    public class BudgetDetailsViewModel
    {
        public Budget Budget { get; set; }
        public decimal SpendingPercentage { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal TotalSpent { get; set; }  // Add this property
        public IEnumerable<Transaction> RecentTransactions { get; set; }
    }
}