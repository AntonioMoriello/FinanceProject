public class EmailSettings
{
    public string SendGridKey { get; set; }
    public string FromEmail { get; set; }
    public string FromName { get; set; }
    public string SmtpHost { get; set; }
    public int SmtpPort { get; set; }
    public string SmtpUsername { get; set; }
    public bool EnableSsl { get; set; }
}