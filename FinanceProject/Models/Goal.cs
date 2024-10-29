using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceManager.Models
{
    public class Goal
    {
        [Key]
        public int GoalId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TargetAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentAmount { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime TargetDate { get; set; }

        public GoalStatus Status { get; set; }

        public GoalType Type { get; set; }

        // Navigation property
        public virtual User User { get; set; }
    }

    public enum GoalStatus
    {
        Active,
        Completed,
        Abandoned
    }

    public enum GoalType
    {
        Saving,
        Debt,
        Investment,
        Other
    }
}