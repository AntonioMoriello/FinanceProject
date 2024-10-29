using Microsoft.EntityFrameworkCore;
using FinanceManager.Models;
using System.Reflection.Emit;

namespace FinanceManager.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<Template> Templates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configurations
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(100);
                entity.Property(e => e.SecurityStamp).HasMaxLength(100);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
            });

            // Transaction configurations
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.TransactionId);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.Date).IsRequired();

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Category configurations
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.CategoryId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(100);
                entity.Property(e => e.ColorCode).HasMaxLength(7);

                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Budget configurations
            modelBuilder.Entity<Budget>(entity =>
            {
                entity.HasKey(e => e.BudgetId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CurrentSpending).HasColumnType("decimal(18,2)");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Budgets)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.Budgets)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Goal configurations
            modelBuilder.Entity<Goal>(entity =>
            {
                entity.HasKey(e => e.GoalId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.TargetAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CurrentAmount).HasColumnType("decimal(18,2)");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Goals)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Template configurations
            modelBuilder.Entity<Template>(entity =>
            {
                entity.HasKey(e => e.TemplateId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.Configuration).IsRequired();
                entity.Property(e => e.IsSystem).HasDefaultValue(false);

                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed default categories
            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    CategoryId = 1,
                    Name = "Housing",
                    Description = "Rent, Mortgage, and Utilities",
                    Type = CategoryType.Expense,
                    ColorCode = "#FF5733",
                    UserId = null,
                    IsSystem = true
                },
                new Category
                {
                    CategoryId = 2,
                    Name = "Food",
                    Description = "Groceries and Dining",
                    Type = CategoryType.Expense,
                    ColorCode = "#33FF57",
                    UserId = null,
                    IsSystem = true
                },
                new Category
                {
                    CategoryId = 3,
                    Name = "Transportation",
                    Description = "Car, Gas, and Public Transit",
                    Type = CategoryType.Expense,
                    ColorCode = "#3357FF",
                    UserId = null,
                    IsSystem = true
                },
                new Category
                {
                    CategoryId = 4,
                    Name = "Salary",
                    Description = "Regular Income",
                    Type = CategoryType.Income,
                    ColorCode = "#57FF33",
                    UserId = null,
                    IsSystem = true
                }
            );

            // Seed default templates
            modelBuilder.Entity<Template>().HasData(
                new Template
                {
                    TemplateId = 1,
                    Name = "Student Budget",
                    Description = "Template for students with common expense categories",
                    Type = TemplateType.Student,
                    Configuration = @"{
                        ""categories"": [
                            {""name"": ""Tuition"", ""type"": ""Expense"", ""color"": ""#FF5733""},
                            {""name"": ""Books"", ""type"": ""Expense"", ""color"": ""#33FF57""},
                            {""name"": ""Housing"", ""type"": ""Expense"", ""color"": ""#3357FF""},
                            {""name"": ""Food"", ""type"": ""Expense"", ""color"": ""#FF33F5""}
                        ],
                        ""budgets"": [
                            {""category"": ""Housing"", ""period"": ""Monthly""},
                            {""category"": ""Food"", ""period"": ""Monthly""}
                        ]
                    }",
                    IsSystem = true
                },
                new Template
                {
                    TemplateId = 2,
                    Name = "Family Budget",
                    Description = "Template for families with household expenses",
                    Type = TemplateType.Family,
                    Configuration = @"{
                        ""categories"": [
                            {""name"": ""Mortgage"", ""type"": ""Expense"", ""color"": ""#FF5733""},
                            {""name"": ""Groceries"", ""type"": ""Expense"", ""color"": ""#33FF57""},
                            {""name"": ""Utilities"", ""type"": ""Expense"", ""color"": ""#3357FF""},
                            {""name"": ""Education"", ""type"": ""Expense"", ""color"": ""#FF33F5""}
                        ],
                        ""budgets"": [
                            {""category"": ""Groceries"", ""period"": ""Monthly""},
                            {""category"": ""Utilities"", ""period"": ""Monthly""}
                        ]
                    }",
                    IsSystem = true
                },
                new Template
                {
                    TemplateId = 3,
                    Name = "Investor Portfolio",
                    Description = "Template for investment tracking",
                    Type = TemplateType.Investor,
                    Configuration = @"{
                        ""categories"": [
                            {""name"": ""Stocks"", ""type"": ""Investment"", ""color"": ""#FF5733""},
                            {""name"": ""Bonds"", ""type"": ""Investment"", ""color"": ""#33FF57""},
                            {""name"": ""Real Estate"", ""type"": ""Investment"", ""color"": ""#3357FF""},
                            {""name"": ""Dividends"", ""type"": ""Income"", ""color"": ""#FF33F5""}
                        ],
                        ""goals"": [
                            {""name"": ""Retirement Fund"", ""type"": ""Investment""},
                            {""name"": ""Emergency Fund"", ""type"": ""Saving""}
                        ]
                    }",
                    IsSystem = true
                }
            );
        }
    }
}