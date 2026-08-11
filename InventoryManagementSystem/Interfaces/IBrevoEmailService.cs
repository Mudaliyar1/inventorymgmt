using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IBrevoEmailService
    {
        Task<(bool Success, string MessageId, string ApiResponse, string ErrorMessage)> SendTransactionalEmailAsync(
            string recipientEmail,
            string subject,
            string htmlContent,
            List<string>? ccRecipients = null);
    }
}
