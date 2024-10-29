using FinanceManager.Models;

namespace FinanceManager.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context)
        {
            // Make sure the database is created
            context.Database.EnsureCreated();

            // Check if there are any records
            if (context.Categories.Any())
            {
                return; // DB has been seeded
            }

            // Add default categories
            var defaultCategories = new Category[]
            {
                new Category { Name = "Housing", Description = "Rent, Mortgage, Utilities", Type = CategoryType.Expense, ColorCode = "#FF5733", IsSystem = true },
                new Category { Name = "Food", Description = "Groceries and Dining", Type = CategoryType.Expense, ColorCode = "#33FF57", IsSystem = true },
                new Category { Name = "Transportation", Description = "Car, Gas, Public Transit", Type = CategoryType.Expense, ColorCode = "#3357FF", IsSystem = true },
                new Category { Name = "Income", Description = "Salary and Other Income", Type = CategoryType.Income, ColorCode = "#57FF33", IsSystem = true }
            };

            context.Categories.AddRange(defaultCategories);
            await context.SaveChangesAsync();
        }
    }
}