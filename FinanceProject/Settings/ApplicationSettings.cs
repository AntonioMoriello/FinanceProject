namespace FinanceManager.Settings
{
    public class ApplicationSettings
    {
        public string SiteName { get; set; }
        public string SupportEmail { get; set; }
        public bool EnableRegistration { get; set; }
        public int MaxLoginAttempts { get; set; }
        public int LockoutDurationMinutes { get; set; }
        public string DefaultCurrency { get; set; }
        public string DefaultDateFormat { get; set; }
        public int SessionTimeoutMinutes { get; set; }
        public PasswordRequirements PasswordRequirements { get; set; }
        public EmailSettings Email { get; set; }
    }

    public class PasswordRequirements
    {
        public int MinimumLength { get; set; }
        public bool RequireDigit { get; set; }
        public bool RequireLowercase { get; set; }
        public bool RequireUppercase { get; set; }
        public bool RequireSpecialCharacter { get; set; }
    }

    public class EmailSettings
    {
        public string SendGridKey { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
    }
}