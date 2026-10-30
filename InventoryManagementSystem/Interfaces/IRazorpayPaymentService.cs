using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IRazorpayPaymentService
    {
        string GetKeyId();
        string GetCurrency();
        bool IsConfigured();

        /// <summary>
        /// Creates a Razorpay Order server-side with integer paise.
        /// </summary>
        Task<(bool Success, string OrderId, string ErrorMessage)> CreateOrderAsync(
            string transactionRef, 
            decimal amountInInr, 
            Dictionary<string, string>? notes = null);

        /// <summary>
        /// Verifies customer payment signature returned by Razorpay Checkout.
        /// </summary>
        bool VerifyPaymentSignature(string orderId, string paymentId, string signature);

        /// <summary>
        /// Verifies Razorpay Webhook signature against configured Webhook secret.
        /// </summary>
        bool VerifyWebhookSignature(string rawBody, string signatureHeader);

        /// <summary>
        /// Fetches payment details from Razorpay to verify capture status.
        /// </summary>
        Task<(bool Success, string Status, decimal AmountInInr, string ErrorMessage)> FetchPaymentDetailsAsync(string paymentId);

        /// <summary>
        /// Fetches all payments associated with a Razorpay Order ID to discover captured payments when customer closes browser.
        /// </summary>
        Task<(bool Success, string PaymentId, string Status, decimal AmountInInr, string Currency, string ErrorMessage)> FetchOrderPaymentsAsync(string orderId);
    }
}
