using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ISaaSPaymentService
    {
        /// <summary>
        /// Initiates a paid package checkout by creating a persistent MongoDB transaction record and a Razorpay order.
        /// </summary>
        Task<(bool Success, SaaSPaymentTransaction? Transaction, string CheckoutOrderId, string RazorpayKeyId, string ErrorMessage)> InitiatePurchaseAsync(
            string packageId,
            string billingCycle,
            string? tenantId,
            string customerName,
            string customerEmail,
            string customerPhone,
            string shopName,
            string purpose = "NewPurchase",
            string? registrationToken = null);

        /// <summary>
        /// Verifies client-submitted Razorpay Checkout signature and triggers subscription activation.
        /// </summary>
        Task<(bool Success, SaaSPaymentTransaction? Transaction, string ErrorMessage)> ProcessPaymentSuccessAsync(
            string razorpayOrderId,
            string razorpayPaymentId,
            string razorpaySignature);

        /// <summary>
        /// Idempotently activates or renews subscription from a verified payment record.
        /// </summary>
        Task<(bool Success, string ErrorMessage)> ActivateSubscriptionFromPaymentAsync(string transactionId);

        /// <summary>
        /// Processes official Razorpay server-to-server webhook events (payment.captured, order.paid).
        /// </summary>
        Task<(bool Success, string Message)> ProcessWebhookAsync(string rawBody, string signatureHeader);

        /// <summary>
        /// Reconciles payments where payment was received but subscription activation is pending.
        /// </summary>
        Task<(int ReconciledCount, int ActivatedCount, int FailedCount)> ReconcilePendingPaymentsAsync();

        /// <summary>
        /// Reconciles an individual transaction with Razorpay using stored Order ID or Payment ID.
        /// Unarchives if payment was captured, marks Paid, and idempotently activates subscription.
        /// </summary>
        Task<(bool Success, string Message, SaaSPaymentTransaction? Transaction)> ReconcileSingleTransactionAsync(string transactionId);

        /// <summary>
        /// Cleans up or archives temporary checkout attempts whose retention period has expired.
        /// Never touches Paid, Paid-but-unactivated, or refund/dispute transactions.
        /// </summary>
        Task<int> CleanupExpiredTemporaryCheckoutsAsync();

        /// <summary>
        /// Records that a customer dismissed or canceled the Razorpay checkout modal.
        /// </summary>
        Task<(bool Success, string Message)> RecordCustomerCheckoutCancellationAsync(string orderIdOrTxnRef, string? reason = null);

        /// <summary>
        /// Allows a Super Admin to manually resolve an unactivated payment recovery case with audit notes.
        /// </summary>
        Task<(bool Success, string Message)> ResolvePaymentCaseManuallyAsync(string transactionId, string resolutionNotes, string adminUser);

        /// <summary>
        /// Retries PDF receipt generation and emailing for an existing transaction.
        /// </summary>
        Task<(bool Success, string ErrorMessage)> RetryReceiptEmailAsync(string transactionId);

        /// <summary>
        /// Retrieves revenue analytics metrics for the Super Admin dashboard.
        /// </summary>
        Task<RevenueMetricsViewModel> GetRevenueMetricsAsync();
    }

    public class RevenueMetricsViewModel
    {
        public decimal TotalVerifiedRevenue { get; set; }
        public decimal ConfirmedRevenue => TotalVerifiedRevenue;
        public decimal TodayRevenue { get; set; }
        public decimal CurrentMonthRevenue { get; set; }
        public decimal CurrentYearRevenue { get; set; }
        public decimal ThisMonthRevenue => CurrentMonthRevenue;
        public decimal ThisYearRevenue => CurrentYearRevenue;
        public long SuccessfulPaymentCount { get; set; }
        public long SuccessfulTransactionsCount => SuccessfulPaymentCount;
        public long PendingPaymentCount { get; set; }
        public long PendingVerificationCount => PendingPaymentCount;
        public long FailedPaymentCount { get; set; }
        public long AwaitingActivationCount { get; set; }
        public long UnactivatedCount => AwaitingActivationCount;
        public long NeedsActivationCount => AwaitingActivationCount;
        public long RecentCheckoutsCount { get; set; }
        public long ArchivedAttemptsCount { get; set; }
        public long RefundDisputeCount { get; set; }
        public decimal RefundTotal { get; set; }
        public decimal NetCollectedRevenue => TotalVerifiedRevenue - RefundTotal;
        public decimal NetRevenue => NetCollectedRevenue;
        public List<SaaSPaymentTransaction> RecentTransactions { get; set; } = new List<SaaSPaymentTransaction>();
        public List<SaaSPaymentTransaction> UnactivatedTransactions { get; set; } = new List<SaaSPaymentTransaction>();
        public List<SaaSPaymentTransaction> ArchivedTransactions { get; set; } = new List<SaaSPaymentTransaction>();
    }
}
