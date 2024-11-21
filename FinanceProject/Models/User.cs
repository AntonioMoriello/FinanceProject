using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceManager.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string PasswordHash { get; set; } = string.Empty;

        public string? SecurityStamp { get; set; }

        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties with explicit delete behavior
        [InverseProperty("User")]
        public virtual ICollection<Transaction> Transactions { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Budget> Budgets { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Goal> Goals { get; set; }

        public User()
        {
            Transactions = new HashSet<Transaction>();
            Budgets = new HashSet<Budget>();
            Goals = new HashSet<Goal>();
        }
    }
}