using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Models
{
    public class Template
    {
        [Key]
        public int TemplateId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        public TemplateType Type { get; set; }

        // Template configuration stored as JSON
        [Required]
        public string Configuration { get; set; }

        public bool IsSystem { get; set; }

        public int? UserId { get; set; }

        // Navigation property
        public virtual User? User { get; set; }
    }

    public enum TemplateType
    {
        Student,
        Family,
        Investor,
        Personal,
        Custom
    }
}