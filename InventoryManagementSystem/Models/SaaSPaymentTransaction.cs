using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class SaaSPaymentTransaction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Associated tenant shop ID. May be empty during direct registration before shop creation.
        /// </summary>
        [BsonElement("TenantId")]
        public string TenantId { get; set; } = string.Empty;

        [BsonElement("ShopName")]
        public string ShopName { get; set; } = string.Empty;

        [BsonElement("PackageId")]
        public string PackageId { get; set; } = string.Empty;

        [BsonElement("PackageName")]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Amount in INR decimal (e.g. 999.00).
        /// </summary>
        [BsonElement("Amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Amount in integer paise for Razorpay integration (e.g. 99900).
        /// </summary>
        [BsonElement("AmountInPaise")]
        public long AmountInPaise { get; set; }

        [BsonElement("Currency")]
        public string Currency { get; set; } = "INR";

        /// <summary>
        /// Unique internal reference (e.g. TXN-20261028-1234).
        /// </summary>
        [BsonElement("TransactionRef")]
        public string TransactionRef { get; set; } = string.Empty;

        [BsonElement("GatewayProvider")]
        public string GatewayProvider { get; set; } = "Razorpay";

        /// <summary>
        /// Separate Payment Status: Created, Pending, PaymentVerificationPending, Paid, Failed, Cancelled, RefundPending, Refunded
        /// </summary>
        [BsonElement("PaymentStatus")]
        public string PaymentStatus { get; set; } = "Created";

        /// <summary>
        /// Separate Subscription Activation Status: Pending, ActivationPending, Activated, Failed
        /// </summary>
        [BsonElement("ActivationStatus")]
        public string ActivationStatus { get; set; } = "Pending";

        /// <summary>
        /// Backward-compatibility alias for existing code referencing Status.
        /// </summary>
        [BsonElement("Status")]
        public string Status
        {
            get => PaymentStatus;
            set => PaymentStatus = value;
        }

        /// <summary>
        /// Payment purpose: NewPurchase, Renewal, Upgrade, Other
        /// </summary>
        [BsonElement("PaymentPurpose")]
        public string PaymentPurpose { get; set; } = "NewPurchase";

        [BsonElement("BillingCycle")]
        public string BillingCycle { get; set; } = "Monthly"; // Monthly, Yearly

        [BsonElement("DurationMonths")]
        public int DurationMonths { get; set; } = 1;

        // Razorpay Gateway Identifiers
        [BsonElement("RazorpayOrderId")]
        public string RazorpayOrderId { get; set; } = string.Empty;

        [BsonElement("RazorpayPaymentId")]
        public string RazorpayPaymentId { get; set; } = string.Empty;

        [BsonElement("RazorpaySignature")]
        public string RazorpaySignature { get; set; } = string.Empty;

        // Customer & Business Details
        [BsonElement("CustomerName")]
        public string CustomerName { get; set; } = string.Empty;

        [BsonElement("CustomerEmail")]
        public string CustomerEmail { get; set; } = string.Empty;

        [BsonElement("CustomerPhone")]
        public string CustomerPhone { get; set; } = string.Empty;

        /// <summary>
        /// Token from PendingRegistration when initiated during registration
        /// </summary>
        [BsonElement("RegistrationToken")]
        public string RegistrationToken { get; set; } = string.Empty;

        // Timestamps (UTC)
        [BsonElement("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("PaymentDate")]
        public DateTime PaymentDate
        {
            get => VerifiedDate ?? CreatedDate;
            set => VerifiedDate = value;
        }

        [BsonElement("VerifiedDate")]
        public DateTime? VerifiedDate { get; set; }

        [BsonElement("ActivationDate")]
        public DateTime? ActivationDate { get; set; }

        // Receipt Details
        [BsonElement("ReceiptNumber")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [BsonElement("ReceiptIssuedAt")]
        public DateTime? ReceiptIssuedAt { get; set; }

        [BsonElement("ReceiptGenerated")]
        public bool ReceiptGenerated { get; set; } = false;

        [BsonElement("ReceiptEmailed")]
        public bool ReceiptEmailed { get; set; } = false;

        [BsonElement("ReceiptPdfRelativePath")]
        public string ReceiptPdfRelativePath { get; set; } = string.Empty;

        [BsonIgnore]
        public byte[]? ReceiptPdfBytes { get; set; }

        // Reconciliation & Error Handling
        [BsonElement("LastReconciliationDate")]
        public DateTime? LastReconciliationDate { get; set; }

        [BsonElement("ReconciliationAttempts")]
        public int ReconciliationAttempts { get; set; } = 0;

        [BsonElement("FailureReason")]
        public string FailureReason { get; set; } = string.Empty;

        [BsonElement("SafeProcessingDetails")]
        public string SafeProcessingDetails { get; set; } = string.Empty;

        [BsonElement("Notes")]
        public string Notes { get; set; } = string.Empty;

        [BsonElement("IdempotencyKey")]
        public string IdempotencyKey { get; set; } = string.Empty;

        // Temporary Checkout Retention & Archiving
        [BsonElement("IsArchived")]
        public bool IsArchived { get; set; } = false;

        [BsonElement("ArchivedAt")]
        public DateTime? ArchivedAt { get; set; }

        [BsonElement("ArchiveReason")]
        public string ArchiveReason { get; set; } = string.Empty;

        [BsonElement("ExpiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [BsonElement("IsCustomerCancelled")]
        public bool IsCustomerCancelled { get; set; } = false;

        [BsonElement("CustomerCancelledAt")]
        public DateTime? CustomerCancelledAt { get; set; }

        // Manual Recovery & Resolution
        [BsonElement("IsManuallyResolved")]
        public bool IsManuallyResolved { get; set; } = false;

        [BsonElement("ResolutionNotes")]
        public string ResolutionNotes { get; set; } = string.Empty;

        [BsonElement("ResolvedAt")]
        public DateTime? ResolvedAt { get; set; }

        [BsonElement("ResolvedBy")]
        public string ResolvedBy { get; set; } = string.Empty;

        // State Transition History (Audit Trail)
        [BsonElement("StateTransitions")]
        public List<PaymentStateTransition> StateTransitions { get; set; } = new List<PaymentStateTransition>();

        [BsonIgnore]
        public string RecordCategory
        {
            get
            {
                if (PaymentStatus == "Paid" && ActivationStatus != "Activated" && !IsManuallyResolved)
                    return "NeedsActivation";
                if (PaymentStatus == "Paid")
                    return "ConfirmedPayment";
                if (PaymentStatus == "RefundPending" || PaymentStatus == "Refunded")
                    return "RefundDispute";
                if (IsArchived)
                    return "ArchivedAttempt";
                return "TemporaryCheckout";
            }
        }

        [BsonIgnore]
        public TimeSpan? AwaitingActivationDuration
        {
            get
            {
                if (PaymentStatus == "Paid" && ActivationStatus != "Activated")
                {
                    var start = VerifiedDate ?? CreatedDate;
                    return DateTime.UtcNow - start;
                }
                return null;
            }
        }
    }

    [BsonIgnoreExtraElements]
    public class PaymentStateTransition
    {
        [BsonElement("TimestampUtc")]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("TimestampIst")]
        public string TimestampIst { get; set; } = string.Empty;

        [BsonElement("FromStatus")]
        public string FromStatus { get; set; } = string.Empty;

        [BsonElement("ToStatus")]
        public string ToStatus { get; set; } = string.Empty;

        [BsonElement("Trigger")]
        public string Trigger { get; set; } = string.Empty; // e.g. "CheckoutInitiated", "ClientSuccessCallback", "WebhookCaptured", "ManualRetry", "RetentionArchived", "LateWebhookRestored"

        [BsonElement("Details")]
        public string Details { get; set; } = string.Empty;
    }
}
