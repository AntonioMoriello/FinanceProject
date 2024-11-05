using System.ComponentModel.DataAnnotations;
using FinanceManager.Models;

namespace FinanceManager.Models.ViewModels
{
    public class ReportFilterViewModel
    {
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        [Display(Name = "Report Type")]
        public ReportType ReportType { get; set; }

        public IEnumerable<Category>? Categories { get; set; }
    }

    public class FinancialSummaryViewModel
    {
        [Display(Name = "Total Income")]
        [DataType(DataType.Currency)]
        public decimal TotalIncome { get; set; }

        [Display(Name = "Total Expenses")]
        [DataType(DataType.Currency)]
        public decimal TotalExpenses { get; set; }

        [Display(Name = "Net Income")]
        [DataType(DataType.Currency)]
        public decimal NetIncome { get; set; }

        public Dictionary<string, decimal> CategoryTotals { get; set; }
        public Dictionary<string, decimal> MonthlyTotals { get; set; }
        public Dictionary<string, decimal> CategoryPercentages { get; set; }
        public IEnumerable<Transaction> RecentTransactions { get; set; }
        public Dictionary<string, decimal> BudgetProgress { get; set; }
        public Dictionary<string, decimal> GoalProgress { get; set; }

        // Added properties
        [Display(Name = "Report Period")]
        public string Period { get; set; }

        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Report Type")]
        public string ReportType { get; set; }

        public FinancialSummaryViewModel()
        {
            CategoryTotals = new Dictionary<string, decimal>();
            MonthlyTotals = new Dictionary<string, decimal>();
            CategoryPercentages = new Dictionary<string, decimal>();
            RecentTransactions = new List<Transaction>();
            BudgetProgress = new Dictionary<string, decimal>();
            GoalProgress = new Dictionary<string, decimal>();
            Period = string.Empty;
            ReportType = string.Empty;
        }

        public string GetDateRangeString()
        {
            if (StartDate.HasValue && EndDate.HasValue)
            {
                return $"{StartDate.Value:MMM d, yyyy} - {EndDate.Value:MMM d, yyyy}";
            }
            return string.Empty;
        }

        public string GetFormattedPeriod()
        {
            if (string.IsNullOrEmpty(Period))
            {
                return GetDateRangeString();
            }
            return Period;
        }
    }

    public class CashFlowReportViewModel
    {
        [Display(Name = "Monthly Income")]
        public Dictionary<string, decimal> MonthlyIncome { get; set; }

        [Display(Name = "Monthly Expenses")]
        public Dictionary<string, decimal> MonthlyExpenses { get; set; }

        [Display(Name = "Monthly Savings")]
        public Dictionary<string, decimal> MonthlySavings { get; set; }

        public List<MonthlyTrend> Trends { get; set; }

        // Added properties
        [Display(Name = "Report Period")]
        public string Period { get; set; }

        [Display(Name = "Total Cash Flow")]
        [DataType(DataType.Currency)]
        public decimal TotalCashFlow { get; set; }

        public CashFlowReportViewModel()
        {
            MonthlyIncome = new Dictionary<string, decimal>();
            MonthlyExpenses = new Dictionary<string, decimal>();
            MonthlySavings = new Dictionary<string, decimal>();
            Trends = new List<MonthlyTrend>();
            Period = string.Empty;
        }
    }

    public class BudgetReportViewModel
    {
        public List<BudgetSummary> BudgetSummaries { get; set; }

        [Display(Name = "Total Budgeted")]
        [DataType(DataType.Currency)]
        public decimal TotalBudgeted { get; set; }

        [Display(Name = "Total Spent")]
        [DataType(DataType.Currency)]
        public decimal TotalSpent { get; set; }

        [Display(Name = "Total Remaining")]
        [DataType(DataType.Currency)]
        public decimal TotalRemaining { get; set; }

        public Dictionary<string, List<decimal>> MonthlySpending { get; set; }

        [Display(Name = "Report Period")]
        public string Period { get; set; }

        public BudgetReportViewModel()
        {
            BudgetSummaries = new List<BudgetSummary>();
            MonthlySpending = new Dictionary<string, List<decimal>>();
            Period = string.Empty;
        }
    }

    public class MonthlyTrend
    {
        [Required]
        public string Month { get; set; }

        [DataType(DataType.Currency)]
        public decimal Income { get; set; }

        [DataType(DataType.Currency)]
        public decimal Expenses { get; set; }

        [DataType(DataType.Currency)]
        public decimal Savings { get; set; }

        [Display(Name = "Savings Rate")]
        [DisplayFormat(DataFormatString = "{0:P2}")]
        public decimal SavingsRate { get; set; }

        public MonthlyTrend()
        {
            Month = string.Empty;
        }
    }

    public class BudgetSummary
    {
        [Required]
        public string Category { get; set; }

        [DataType(DataType.Currency)]
        public decimal Budgeted { get; set; }

        [DataType(DataType.Currency)]
        public decimal Spent { get; set; }

        [DataType(DataType.Currency)]
        public decimal Remaining { get; set; }

        [Display(Name = "Usage %")]
        [DisplayFormat(DataFormatString = "{0:P2}")]
        public decimal UsagePercentage { get; set; }

        public BudgetSummary()
        {
            Category = string.Empty;
        }
    }

    public enum ReportType
    {
        [Display(Name = "Financial Summary")]
        FinancialSummary,

        [Display(Name = "Cash Flow")]
        CashFlow,

        [Display(Name = "Budget Analysis")]
        BudgetAnalysis,

        [Display(Name = "Category Breakdown")]
        CategoryBreakdown,

        [Display(Name = "Savings Progress")]
        SavingsProgress
    }
}