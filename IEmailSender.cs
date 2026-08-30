namespace VendorShopOnline.Services
{
    /// <summary>
    /// Abstraction over email delivery so controllers never depend on a
    /// concrete SMTP implementation (Dependency Inversion — SOLID).
    /// </summary>
    public interface IEmailSender
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}
