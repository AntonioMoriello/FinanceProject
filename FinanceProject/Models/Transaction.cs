using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceManager.Models
{
    public class Transaction
    {
        [Key]
        public int TransactionId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        public bool IsRecurring { get; set; }

        public string? RecurrencePattern { get; set; }

        public DateTime? NextRecurrenceDate { get; set; }

        public TransactionType Type { get; set; }

        // Navigation properties
        public virtual User User { get; set; }
        public virtual Category Category { get; set; }
    }

    public enum TransactionType
    {
        Expense,
        Income
    }
}