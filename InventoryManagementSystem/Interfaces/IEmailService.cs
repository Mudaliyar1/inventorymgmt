using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
        Task SendWelcomeEmailAsync(string toEmail, string username);
        Task SendForgotPasswordEmailAsync(string toEmail, string resetLink);
        Task SendPasswordChangedEmailAsync(string toEmail, string username);
        Task SendLowStockAlertEmailAsync(string toEmail, string productName, int currentStock, int minStock);
        Task SendNewUserCreatedEmailAsync(string toEmail, string username, string role, string tempPassword);
    }
}
