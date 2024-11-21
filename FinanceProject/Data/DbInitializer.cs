using Microsoft.EntityFrameworkCore;
using FinanceManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinanceManager.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context)
        {
            // Add default categories if they don't exist
            if (!await context.Categories.AnyAsync())
            {
                var defaultCategories = new List<Category>
                {
                    new Category
                    {
                        Name = "Housing",
                        Description = "Rent, Mortgage, Utilities",
                        Type = CategoryType.Expense,
                        ColorCode = "#FF5733",
                        IsSystem = true
                    },
                    new Category
                    {
                        Name = "Food",
                        Description = "Groceries and Dining",
                        Type = CategoryType.Expense,
                        ColorCode = "#33FF57",
                        IsSystem = true
                    },
                    new Category
                    {
                        Name = "Transportation",
                        Description = "Car, Gas, Public Transit",
                        Type = CategoryType.Expense,
                        ColorCode = "#3357FF",
                        IsSystem = true
                    },
                    new Category
                    {
                        Name = "Income",
                        Description = "Salary and Other Income",
                        Type = CategoryType.Income,
                        ColorCode = "#57FF33",
                        IsSystem = true
                    }
                };

                await context.Categories.AddRangeAsync(defaultCategories);
                await context.SaveChangesAsync();
            }
        }
    }
}