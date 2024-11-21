using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using FinanceManager.Models;
using FinanceManager.Models.ViewModels;
using FinanceManager.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAccountService accountService,
            IEmailService emailService,
            ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Register()
        {
            _logger.LogInformation("Register page accessed");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            _logger.LogInformation("Registration attempt for email: {Email}", model.Email);

            // Log the model state
            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        _logger.LogWarning("ModelState Error: {Error}", error.ErrorMessage);
                    }
                }
                return View(model);
            }

            try
            {
                var user = new User
                {
                    Username = model.Username,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                _logger.LogInformation("Attempting to register user: {Username}", user.Username);
                var (success, message, userId) = await _accountService.RegisterUserAsync(user, model.Password);

                if (success)
                {
                    _logger.LogInformation("User registered successfully. UserId: {UserId}", userId);

                    // Try to send welcome email
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(user.Email, user.Username);
                        _logger.LogInformation("Welcome email sent successfully to: {Email}", user.Email);
                    }
                    catch (Exception emailEx)
                    {
                        // Log but don't fail registration if email fails
                        _logger.LogError(emailEx, "Error sending welcome email to {Email}", user.Email);
                    }

                    // Create claims
                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        // Optional: Configure authentication properties
                        IsPersistent = true, // Remember the user
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) // Cookie expires in 30 days
                    };

                    // Sign in the user
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    _logger.LogInformation("User signed in successfully: {UserId}", userId);
                    TempData["SuccessMessage"] = "Registration successful! Welcome to Finance Manager!";

                    // Optionally add email status to success message
                    if (!string.IsNullOrEmpty(user.Email))
                    {
                        TempData["InfoMessage"] = "Check your email for additional information.";
                    }

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    _logger.LogWarning("Registration failed: {Message}", message);
                    ModelState.AddModelError("", message);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", model.Email);
                ModelState.AddModelError("", "An unexpected error occurred during registration. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var (success, user) = await _accountService.ValidateUserAsync(model.Email, model.Password);

                if (success)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid login attempt.");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _accountService.GetUserByEmailAsync(model.Email);
                    if (user != null)
                    {
                        var token = _accountService.GeneratePasswordResetToken();

                        // Generate reset link
                        var resetLink = Url.Action(
                            "ResetPassword",
                            "Account",
                            new { email = model.Email, code = token },
                            protocol: Request.Scheme);

                        // Send email
                        await _emailService.SendPasswordResetEmailAsync(model.Email, resetLink);

                        _logger.LogInformation("Password reset email sent to {Email}", model.Email);
                    }

                    // Always redirect to confirmation page to prevent email enumeration
                    return RedirectToAction(nameof(ForgotPasswordConfirmation));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending password reset email to {Email}", model.Email);
                    ModelState.AddModelError("", "Error sending password reset email. Please try again later.");
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string code)
        {
            if (email == null || code == null)
            {
                return BadRequest("Invalid password reset token");
            }
            var model = new ResetPasswordViewModel { Email = email, Code = code };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _accountService.ValidatePasswordResetTokenAsync(model.Email, model.Code))
                {
                    if (await _accountService.ResetPasswordAsync(model.Email, model.Password))
                    {
                        return RedirectToAction(nameof(ResetPasswordConfirmation));
                    }
                }
                ModelState.AddModelError("", "Invalid password reset attempt.");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // Confirmation action methods
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }
    }
}