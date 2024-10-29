using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinanceManager.Models;

namespace FinanceManager.Models.ViewModels
{
    public class TransactionCreateViewModel
    {
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        [StringLength(200)]
        public string Description { get; set; }

        [Display(Name = "Transaction Type")]
        public TransactionType Type { get; set; }

        [Display(Name = "Is Recurring?")]
        public bool IsRecurring { get; set; }

        [Display(Name = "Recurrence Pattern")]
        public string? RecurrencePattern { get; set; }

        public IEnumerable<Category>? Categories { get; set; }
    }

    public class TransactionEditViewModel : TransactionCreateViewModel
    {
        public int TransactionId { get; set; }
    }

    public class TransactionListViewModel
    {
        public IEnumerable<Transaction> Transactions { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetAmount { get; set; }
        public Dictionary<string, decimal> CategoryTotals { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CategoryId { get; set; }
        public TransactionType? Type { get; set; }

        public IEnumerable<Category>? Categories { get; set; }
    }
}