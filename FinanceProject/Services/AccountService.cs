using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;
using System.Text;

namespace FinanceManager.Services
{
    public interface IAccountService
    {
        Task<(bool success, string message, int userId)> RegisterUserAsync(User user, string password);
        Task<(bool success, User user)> ValidateUserAsync(string email, string password);
        Task<bool> CheckEmailExistsAsync(string email);
        Task<User> GetUserByEmailAsync(string email);
        string GeneratePasswordResetToken();
        Task<bool> ValidatePasswordResetTokenAsync(string email, string token);
        Task<bool> ResetPasswordAsync(string email, string newPassword);
    }

    public class AccountService : IAccountService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountService> _logger;

        public AccountService(ApplicationDbContext context, ILogger<AccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool success, string message, int userId)> RegisterUserAsync(User user, string password)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate unique email
                if (await _context.Users.AnyAsync(u => u.Email == user.Email))
                    return (false, "Email already exists", 0);

                // Validate unique username
                if (await _context.Users.AnyAsync(u => u.Username == user.Username))
                    return (false, "Username already exists", 0);

                // Hash password
                user.PasswordHash = HashPassword(password);
                user.SecurityStamp = Guid.NewGuid().ToString();
                user.CreatedDate = DateTime.UtcNow;
                user.IsActive = true;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Create default categories for the user
                var defaultCategories = new[]
                {
                new Category { Name = "Entertainment", Type = CategoryType.Expense, UserId = user.UserId },
                new Category { Name = "Healthcare", Type = CategoryType.Expense, UserId = user.UserId },
                new Category { Name = "Shopping", Type = CategoryType.Expense, UserId = user.UserId }
            };

                _context.Categories.AddRange(defaultCategories);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Registration successful", user.UserId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error registering user: {Email}", user.Email);
                return (false, "Registration failed. Please try again.", 0);
            }
        }

        public async Task<(bool success, User user)> ValidateUserAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return (false, null);

            var passwordHash = HashPassword(password);
            if (user.PasswordHash != passwordHash) return (false, null);

            return (true, user);
        }

        public async Task<bool> CheckEmailExistsAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public string GeneratePasswordResetToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        public async Task<bool> ValidatePasswordResetTokenAsync(string email, string token)
        {
            var user = await GetUserByEmailAsync(email);
            return user?.SecurityStamp == token;
        }

        public async Task<bool> ResetPasswordAsync(string email, string newPassword)
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null) return false;

            user.PasswordHash = HashPassword(newPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();

            await _context.SaveChangesAsync();
            return true;
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

    }
}
