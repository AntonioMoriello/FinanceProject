using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        public int? UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        [StringLength(100)]
        public string? Description { get; set; }

        [StringLength(7)]
        public string? ColorCode { get; set; }

        public CategoryType Type { get; set; }

        public bool IsSystem { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }
        public virtual ICollection<Budget> Budgets { get; set; }

        public Category()
        {
            Transactions = new HashSet<Transaction>();
            Budgets = new HashSet<Budget>();
        }
    }

    public enum CategoryType
    {
        Expense,
        Income,
        Investment
    }
}