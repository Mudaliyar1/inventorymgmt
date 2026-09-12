using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
        Task SendEmailWithAttachmentAsync(string toEmail, string subject, string htmlMessage, byte[] attachmentBytes, string attachmentFileName, string contentType = "application/pdf");
        Task SendInvoiceEmailAsync(string toEmail, Sale sale, byte[] pdfBytes);
        Task SendWelcomeEmailAsync(string toEmail, string username);
        Task SendForgotPasswordEmailAsync(string toEmail, string resetLink);
        Task SendPasswordResetOtpEmailAsync(string toEmail, string otpCode, string recipientName);
        Task SendPasswordChangedEmailAsync(string toEmail, string username);
        Task SendLowStockAlertEmailAsync(string toEmail, string productName, int currentStock, int minStock);
        Task SendNewUserCreatedEmailAsync(string toEmail, string username, string role, string tempPassword);
    }
}
