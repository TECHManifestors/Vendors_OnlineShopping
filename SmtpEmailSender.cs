using System.Net;
using System.Net.Mail;

namespace VendorShopOnline.Services
{
    /// <summary>
    /// Sends real email via SMTP using settings from appsettings.json /
    /// user-secrets ("EmailSettings" section). Intended for production use
    /// once real SMTP credentials (e.g. a Gmail app password, SendGrid, or
    /// the institution's mail relay) are supplied.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var host = _configuration["EmailSettings:SmtpHost"];
            var portString = _configuration["EmailSettings:SmtpPort"];
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            var fromAddress = _configuration["EmailSettings:FromAddress"] ?? username;
            var fromName = _configuration["EmailSettings:FromName"] ?? "VendorShop Online";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
            {
                // Fail safely rather than crashing the request pipeline —
                // the calling service falls back to logging in this case.
                _logger.LogWarning("SMTP settings are not configured. Email to {ToEmail} was not sent.", toEmail);
                throw new InvalidOperationException("SMTP email settings are not configured.");
            }

            var port = int.TryParse(portString, out var parsedPort) ? parsedPort : 587;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromAddress!, fromName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Password reset email sent to {ToEmail}.", toEmail);
        }
    }

    /// <summary>
    /// Development fallback: writes the email content to the application
    /// log instead of sending real mail. This lets the full forgot-password
    /// workflow be demonstrated and tested end-to-end (including the actual
    /// reset link) without requiring a real mail server during marking.
    /// Swap for SmtpEmailSender in Program.cs once real credentials exist.
    /// </summary>
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            _logger.LogInformation(
                "----- DEV EMAIL (no SMTP configured) -----\nTo: {ToEmail}\nSubject: {Subject}\nBody:\n{Body}\n-------------------------------------------",
                toEmail, subject, htmlMessage);
            return Task.CompletedTask;
        }
    }
}
