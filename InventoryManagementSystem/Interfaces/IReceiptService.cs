using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IReceiptService
    {
        /// <summary>
        /// Generates a professional PDF payment receipt for a verified subscription payment.
        /// </summary>
        byte[] GeneratePaymentReceiptPdf(SaaSPaymentTransaction transaction, SubscriptionPackage? package, PlatformSettings? platformSettings);

        /// <summary>
        /// Generates a unique receipt number in format RCP-YYYYMMDD-XXXX.
        /// </summary>
        string GenerateReceiptNumber();
    }
}
