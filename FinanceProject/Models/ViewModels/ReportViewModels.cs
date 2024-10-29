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
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public Dictionary<string, decimal> CategoryTotals { get; set; }
        public Dictionary<string, decimal> MonthlyTotals { get; set; }
        public Dictionary<string, decimal> CategoryPercentages { get; set; }
        public IEnumerable<Transaction> RecentTransactions { get; set; }
        public Dictionary<string, decimal> BudgetProgress { get; set; }
        public Dictionary<string, decimal> GoalProgress { get; set; }
    }

    public class CashFlowReportViewModel
    {
        public Dictionary<string, decimal> MonthlyIncome { get; set; }
        public Dictionary<string, decimal> MonthlyExpenses { get; set; }
        public Dictionary<string, decimal> MonthlySavings { get; set; }
        public List<MonthlyTrend> Trends { get; set; }
    }

    public class BudgetReportViewModel
    {
        public List<BudgetSummary> BudgetSummaries { get; set; }
        public decimal TotalBudgeted { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalRemaining { get; set; }
        public Dictionary<string, List<decimal>> MonthlySpending { get; set; }
    }

    public class MonthlyTrend
    {
        public string Month { get; set; }
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Savings { get; set; }
        public decimal SavingsRate { get; set; }
    }

    public class BudgetSummary
    {
        public string Category { get; set; }
        public decimal Budgeted { get; set; }
        public decimal Spent { get; set; }
        public decimal Remaining { get; set; }
        public decimal UsagePercentage { get; set; }
    }

    public enum ReportType
    {
        FinancialSummary,
        CashFlow,
        BudgetAnalysis,
        CategoryBreakdown,
        SavingsProgress
    }
}