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
            try
            {
                // First check if database connection is working
                if (!await _context.Database.CanConnectAsync())
                {
                    _logger.LogError("Cannot connect to database");
                    return (false, "Unable to connect to database. Please contact administrator.", 0);
                }

                // Create an execution strategy
                var strategy = _context.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    // Check for existing email and username before starting transaction
                    var existingEmail = await _context.Users
                        .AnyAsync(u => u.Email.ToLower() == user.Email.ToLower());
                    if (existingEmail)
                    {
                        _logger.LogWarning("Registration attempt with existing email: {Email}", user.Email);
                        return (false, "This email address is already registered.", 0);
                    }

                    var existingUsername = await _context.Users
                        .AnyAsync(u => u.Username.ToLower() == user.Username.ToLower());
                    if (existingUsername)
                    {
                        _logger.LogWarning("Registration attempt with existing username: {Username}", user.Username);
                        return (false, "This username is already taken.", 0);
                    }

                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            // Hash password and set user properties
                            user.PasswordHash = HashPassword(password);
                            user.SecurityStamp = Guid.NewGuid().ToString();
                            user.CreatedDate = DateTime.UtcNow;
                            user.IsActive = true;

                            // Add user
                            _context.Users.Add(user);
                            await _context.SaveChangesAsync();

                            // Create default categories for the user
                            var defaultCategories = new[]
                            {
                        new Category {
                            Name = "Entertainment",
                            Description = "Entertainment expenses",
                            Type = CategoryType.Expense,
                            UserId = user.UserId,
                            ColorCode = "#FF4081"
                        },
                        new Category {
                            Name = "Healthcare",
                            Description = "Healthcare expenses",
                            Type = CategoryType.Expense,
                            UserId = user.UserId,
                            ColorCode = "#2196F3"
                        },
                        new Category {
                            Name = "Shopping",
                            Description = "Shopping expenses",
                            Type = CategoryType.Expense,
                            UserId = user.UserId,
                            ColorCode = "#4CAF50"
                        }
                    };

                            _context.Categories.AddRange(defaultCategories);
                            await _context.SaveChangesAsync();

                            await transaction.CommitAsync();

                            _logger.LogInformation("User registered successfully: {UserId}", user.UserId);
                            return (true, "Registration successful", user.UserId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during registration transaction for user {Email}", user.Email);
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user: {Email}", user.Email);
                return (false, "An error occurred during registration. Please try again.", 0);
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
