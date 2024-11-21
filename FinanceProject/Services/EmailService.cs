using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Options;
using FinanceManager.Settings;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading.Tasks;

namespace FinanceManager.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = false);
        Task SendPasswordResetEmailAsync(string to, string resetLink);
        Task SendWelcomeEmailAsync(string to, string username);
        Task SendAccountLockedEmailAsync(string to);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            try
            {
                var message = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };
                message.To.Add(to);

                using var client = new SmtpClient();
                // Configure SMTP client based on your email provider
                // For SendGrid:
                client.Host = "smtp.sendgrid.net";
                client.Port = 587;
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential("apikey", _emailSettings.SendGridKey);

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent successfully to {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {To}", to);
                throw;
            }
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            var subject = "Reset Your Password - Finance Manager";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                    <h2>Reset Your Password</h2>
                    <p>You've requested to reset your password. Click the link below to proceed:</p>
                    <p><a href='{resetLink}' style='padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Reset Password</a></p>
                    <p>If you didn't request this, please ignore this email.</p>
                    <p>The link will expire in 24 hours.</p>
                    <p>Best regards,<br>Finance Manager Team</p>
                </body>
                </html>";

            await SendEmailAsync(to, subject, body, true);
        }

        public async Task SendWelcomeEmailAsync(string to, string username)
        {
            var subject = "Welcome to Finance Manager!";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                    <h2>Welcome to Finance Manager, {username}!</h2>
                    <p>Thank you for joining us. Here are some tips to get started:</p>
                    <ul>
                        <li>Set up your budget categories</li>
                        <li>Create your first financial goal</li>
                        <li>Track your daily expenses</li>
                    </ul>
                    <p>If you have any questions, feel free to contact our support team.</p>
                    <p>Best regards,<br>Finance Manager Team</p>
                </body>
                </html>";

            await SendEmailAsync(to, subject, body, true);
        }

        public async Task SendAccountLockedEmailAsync(string to)
        {
            var subject = "Account Security Alert - Finance Manager";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                    <h2>Account Security Alert</h2>
                    <p>Your account has been temporarily locked due to multiple failed login attempts.</p>
                    <p>If this wasn't you, please contact our support team immediately.</p>
                    <p>Your account will be automatically unlocked after 30 minutes.</p>
                    <p>Best regards,<br>Finance Manager Team</p>
                </body>
                </html>";

            await SendEmailAsync(to, subject, body, true);
        }
    }
}