using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class SaaSPaymentService : ISaaSPaymentService
    {
        private readonly MongoDbContext _context;
        private readonly IRazorpayPaymentService _razorpayService;
        private readonly ILicenseService _licenseService;
        private readonly IReceiptService _receiptService;
        private readonly IEmailService _emailService;
        private readonly IPlatformSettingsService _platformSettingsService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<SaaSPaymentService> _logger;

        public SaaSPaymentService(
            MongoDbContext context,
            IRazorpayPaymentService razorpayService,
            ILicenseService licenseService,
            IReceiptService receiptService,
            IEmailService emailService,
            IPlatformSettingsService platformSettingsService,
            IAuditLogService auditLogService,
            ILogger<SaaSPaymentService> logger)
        {
            _context = context;
            _razorpayService = razorpayService;
            _licenseService = licenseService;
            _receiptService = receiptService;
            _emailService = emailService;
            _platformSettingsService = platformSettingsService;
            _auditLogService = auditLogService;
            _logger = logger;
        }

        public async Task<(bool Success, SaaSPaymentTransaction? Transaction, string CheckoutOrderId, string RazorpayKeyId, string ErrorMessage)> InitiatePurchaseAsync(
            string packageId,
            string billingCycle,
            string? tenantId,
            string customerName,
            string customerEmail,
            string customerPhone,
            string shopName,
            string purpose = "NewPurchase",
            string? registrationToken = null)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return (false, null, string.Empty, string.Empty, "Subscription package ID is required.");
            }

            var package = await _context.SubscriptionPackages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
            if (package == null || !package.IsActive)
            {
                return (false, null, string.Empty, string.Empty, "Selected subscription package is not active or available for purchase.");
            }

            bool isYearly = string.Equals(billingCycle?.Trim(), "Yearly", StringComparison.OrdinalIgnoreCase);
            decimal packagePrice = isYearly 
                ? (package.YearlyPrice > 0 ? package.YearlyPrice : package.MonthlyPrice * 12)
                : package.MonthlyPrice;

            if (packagePrice <= 0)
            {
                return (false, null, string.Empty, string.Empty, "Invalid package price configuration.");
            }

            var istNow = DateTime.UtcNow.AddMinutes(330);
            var randomSuffix = Random.Shared.Next(1000, 9999);
            var txnRef = $"TXN-{istNow:yyyyMMdd}-{randomSuffix}";

            var platformSettings = await _platformSettingsService.GetSettingsAsync();
            var retentionSpan = platformSettings.GetTemporaryCheckoutRetentionTimeSpan();
            var expiresAt = DateTime.UtcNow.Add(retentionSpan);

            var transaction = new SaaSPaymentTransaction
            {
                TransactionRef = txnRef,
                TenantId = tenantId ?? string.Empty,
                ShopName = !string.IsNullOrWhiteSpace(shopName) ? shopName.Trim() : (tenantId ?? string.Empty),
                PackageId = package.Id,
                PackageName = package.Name,
                Amount = packagePrice,
                AmountInPaise = (long)Math.Round(packagePrice * 100m, MidpointRounding.AwayFromZero),
                Currency = _razorpayService.GetCurrency(),
                GatewayProvider = "Razorpay",
                PaymentStatus = "Created",
                ActivationStatus = "Pending",
                PaymentPurpose = !string.IsNullOrWhiteSpace(purpose) ? purpose : "NewPurchase",
                BillingCycle = isYearly ? "Yearly" : "Monthly",
                DurationMonths = isYearly ? 12 : 1,
                CustomerName = customerName?.Trim() ?? string.Empty,
                CustomerEmail = customerEmail?.Trim() ?? string.Empty,
                CustomerPhone = customerPhone?.Trim() ?? string.Empty,
                RegistrationToken = registrationToken ?? string.Empty,
                CreatedDate = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                Notes = $"Package: {package.Name} ({billingCycle})"
            };

            transaction.StateTransitions.Add(new PaymentStateTransition
            {
                TimestampUtc = DateTime.UtcNow,
                TimestampIst = istNow.ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                FromStatus = "None",
                ToStatus = "Created",
                Trigger = "CheckoutInitiated",
                Details = $"Customer initialized Razorpay checkout order for {package.Name} ({billingCycle}). Retention expiry set to {expiresAt.AddMinutes(330):dd MMM yyyy, hh:mm:ss tt IST}."
            });

            // 1. Persistent pending record in MongoDB BEFORE contacting payment gateway
            await _context.SaaSPaymentTransactions.InsertOneAsync(transaction);

            // 2. Create Razorpay order
            var notes = new Dictionary<string, string>
            {
                { "txn_ref", txnRef },
                { "package_id", package.Id },
                { "package_name", package.Name },
                { "tenant_id", tenantId ?? string.Empty },
                { "purpose", transaction.PaymentPurpose }
            };

            var (orderSuccess, orderId, orderError) = await _razorpayService.CreateOrderAsync(txnRef, packagePrice, notes);
            if (!orderSuccess)
            {
                transaction.PaymentStatus = "Failed";
                transaction.FailureReason = orderError;
                transaction.StateTransitions.Add(new PaymentStateTransition
                {
                    TimestampUtc = DateTime.UtcNow,
                    TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                    FromStatus = "Created",
                    ToStatus = "Failed",
                    Trigger = "RazorpayOrderCreationFailed",
                    Details = $"Razorpay order generation failed: {orderError}"
                });
                await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);
                return (false, transaction, string.Empty, string.Empty, orderError);
            }

            // 3. Associate Razorpay Order ID and update to Pending
            transaction.RazorpayOrderId = orderId;
            transaction.PaymentStatus = "Pending";
            transaction.StateTransitions.Add(new PaymentStateTransition
            {
                TimestampUtc = DateTime.UtcNow,
                TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                FromStatus = "Created",
                ToStatus = "Pending",
                Trigger = "RazorpayOrderCreated",
                Details = $"Razorpay Order {orderId} generated successfully. Awaiting payment capture."
            });
            await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

            _logger.LogInformation("[PAYMENT] Initialized transaction {TxnRef} with Razorpay Order {OrderId} for {Customer}",
                txnRef, orderId, transaction.CustomerEmail);

            return (true, transaction, orderId, _razorpayService.GetKeyId(), string.Empty);
        }

        public async Task<(bool Success, SaaSPaymentTransaction? Transaction, string ErrorMessage)> ProcessPaymentSuccessAsync(
            string razorpayOrderId,
            string razorpayPaymentId,
            string razorpaySignature)
        {
            if (string.IsNullOrWhiteSpace(razorpayOrderId))
            {
                return (false, null, "Razorpay Order ID is missing.");
            }

            var transaction = await _context.SaaSPaymentTransactions
                .Find(t => t.RazorpayOrderId == razorpayOrderId)
                .FirstOrDefaultAsync();

            if (transaction == null)
            {
                _logger.LogWarning("[PAYMENT] Order ID {OrderId} not found in transaction records.", razorpayOrderId);
                return (false, null, "Payment order record not found in system.");
            }

            // Verify payment signature
            bool isSignatureValid = _razorpayService.VerifyPaymentSignature(razorpayOrderId, razorpayPaymentId, razorpaySignature);
            if (!isSignatureValid)
            {
                var prevStatus = transaction.PaymentStatus;
                transaction.PaymentStatus = "PaymentVerificationPending";
                transaction.FailureReason = "Razorpay checkout signature verification failed.";
                transaction.StateTransitions.Add(new PaymentStateTransition
                {
                    TimestampUtc = DateTime.UtcNow,
                    TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                    FromStatus = prevStatus,
                    ToStatus = "PaymentVerificationPending",
                    Trigger = "SignatureVerificationFailed",
                    Details = "Client signature could not be verified against Razorpay secret."
                });
                await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

                _logger.LogError("[PAYMENT] Invalid signature received for transaction {TxnRef}, order {OrderId}.", 
                    transaction.TransactionRef, razorpayOrderId);

                return (false, transaction, "Payment signature verification failed. Please contact support.");
            }

            // If the transaction was previously archived due to retention expiry, restore it immediately!
            if (transaction.IsArchived)
            {
                transaction.IsArchived = false;
                transaction.ArchiveReason = string.Empty;
                transaction.ArchivedAt = null;
                _logger.LogInformation("[PAYMENT RECOVERY] Restoring previously archived checkout attempt {TxnRef} because payment was confirmed.", transaction.TransactionRef);
            }

            // Update to Paid state
            var oldStatus = transaction.PaymentStatus;
            transaction.RazorpayPaymentId = razorpayPaymentId;
            transaction.RazorpaySignature = razorpaySignature;
            transaction.PaymentStatus = "Paid";
            transaction.VerifiedDate = DateTime.UtcNow;

            transaction.StateTransitions.Add(new PaymentStateTransition
            {
                TimestampUtc = DateTime.UtcNow,
                TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                FromStatus = oldStatus,
                ToStatus = "Paid",
                Trigger = "ClientSignatureVerified",
                Details = $"Razorpay payment {razorpayPaymentId} verified successfully via checkout signature."
            });
            await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

            _logger.LogInformation("[PAYMENT] Verified payment {PaymentId} for transaction {TxnRef}. Initiating subscription activation.",
                razorpayPaymentId, transaction.TransactionRef);

            // Trigger idempotent subscription activation
            var (activationSuccess, activationError) = await ActivateSubscriptionFromPaymentAsync(transaction.Id);
            if (!activationSuccess)
            {
                _logger.LogWarning("[PAYMENT] Payment {TxnRef} is PAID, but activation pending: {Error}", 
                    transaction.TransactionRef, activationError);
            }

            // Refresh transaction object
            var refreshed = await _context.SaaSPaymentTransactions.Find(t => t.Id == transaction.Id).FirstOrDefaultAsync();
            return (true, refreshed ?? transaction, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> ActivateSubscriptionFromPaymentAsync(string transactionId)
        {
            var transaction = await _context.SaaSPaymentTransactions.Find(t => t.Id == transactionId).FirstOrDefaultAsync();
            if (transaction == null)
            {
                return (false, "Transaction not found.");
            }

            // Must be verified/paid before activating
            if (transaction.PaymentStatus != "Paid")
            {
                return (false, $"Cannot activate subscription: payment status is '{transaction.PaymentStatus}'. Must be 'Paid'.");
            }

            // Idempotency: Prevent duplicate activation
            if (transaction.ActivationStatus == "Activated")
            {
                _logger.LogInformation("[PAYMENT] Transaction {TxnRef} is already activated. Skipping duplicate activation.", transaction.TransactionRef);
                return (true, string.Empty);
            }

            var prevActStatus = transaction.ActivationStatus;
            transaction.ActivationStatus = "ActivationPending";
            transaction.StateTransitions.Add(new PaymentStateTransition
            {
                TimestampUtc = DateTime.UtcNow,
                TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                FromStatus = prevActStatus,
                ToStatus = "ActivationPending",
                Trigger = "ActivationInitiated",
                Details = "Starting subscription provisioning and workspace license synchronization."
            });
            await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

            try
            {
                var package = await _context.SubscriptionPackages.Find(p => p.Id == transaction.PackageId).FirstOrDefaultAsync();
                int durationDays = transaction.BillingCycle == "Yearly" ? 365 : (transaction.DurationMonths * 30);
                if (durationDays <= 0) durationDays = 30;

                string activeTenantId = transaction.TenantId;

                // Case 1: Direct Registration (Tenant has not been created yet)
                if (string.IsNullOrEmpty(activeTenantId) && !string.IsNullOrEmpty(transaction.RegistrationToken))
                {
                    var pending = await _context.PendingRegistrations
                        .Find(p => p.RegistrationToken == transaction.RegistrationToken)
                        .FirstOrDefaultAsync();

                    if (pending != null)
                    {
                        var tenantCount = await _context.Tenants.CountDocumentsAsync(_ => true);
                        var tenantCode = $"SHOP-{100 + tenantCount + 1}";

                        var tenant = new Tenant
                        {
                            TenantCode = tenantCode,
                            ShopName = pending.ShopName,
                            OwnerName = pending.OwnerName,
                            ContactEmail = pending.ContactEmail,
                            Phone = pending.Phone,
                            Address = pending.Address,
                            GSTIN = pending.GSTIN,
                            PackageId = transaction.PackageId,
                            Status = "Active", // Direct paid package is Active immediately
                            IsPaidCustomer = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await _context.Tenants.InsertOneAsync(tenant);

                        var shopAdmin = new User
                        {
                            TenantId = tenant.Id,
                            Username = pending.ContactEmail,
                            Email = pending.ContactEmail,
                            FullName = pending.OwnerName,
                            PhoneNumber = pending.Phone,
                            Role = Role.Admin,
                            EmployeeId = "EMP-0001",
                            IsLocked = false,
                            PasswordHash = pending.PasswordHash,
                            CreatedDate = DateTime.UtcNow,
                            UpdatedDate = DateTime.UtcNow
                        };
                        await _context.Users.InsertOneAsync(shopAdmin);

                        activeTenantId = tenant.Id;
                        transaction.TenantId = tenant.Id;
                        transaction.ShopName = tenant.ShopName;

                        // Cleanup pending registration
                        await _context.PendingRegistrations.DeleteOneAsync(p => p.Id == pending.Id);
                    }
                }

                // Fallback: If activeTenantId is still null, look up by CustomerEmail
                if (string.IsNullOrEmpty(activeTenantId) && !string.IsNullOrWhiteSpace(transaction.CustomerEmail))
                {
                    var existingTenant = await _context.Tenants
                        .Find(t => t.ContactEmail == transaction.CustomerEmail)
                        .FirstOrDefaultAsync();

                    if (existingTenant != null)
                    {
                        activeTenantId = existingTenant.Id;
                        transaction.TenantId = existingTenant.Id;
                        if (string.IsNullOrEmpty(transaction.ShopName)) transaction.ShopName = existingTenant.ShopName;
                    }
                    else
                    {
                        // Fallback: Create Tenant from transaction data so paid customer never loses access
                        var tenantCount = await _context.Tenants.CountDocumentsAsync(_ => true);
                        var tenantCode = $"SHOP-{100 + tenantCount + 1}";
                        var newTenant = new Tenant
                        {
                            TenantCode = tenantCode,
                            ShopName = !string.IsNullOrWhiteSpace(transaction.ShopName) ? transaction.ShopName : "Shop " + tenantCode,
                            OwnerName = !string.IsNullOrWhiteSpace(transaction.CustomerName) ? transaction.CustomerName : "Store Owner",
                            ContactEmail = transaction.CustomerEmail,
                            Phone = transaction.CustomerPhone ?? string.Empty,
                            PackageId = transaction.PackageId,
                            Status = "Active",
                            IsPaidCustomer = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await _context.Tenants.InsertOneAsync(newTenant);
                        activeTenantId = newTenant.Id;
                        transaction.TenantId = newTenant.Id;

                        var existingUser = await _context.Users.Find(u => u.Email == transaction.CustomerEmail).FirstOrDefaultAsync();
                        if (existingUser != null)
                        {
                            existingUser.TenantId = newTenant.Id;
                            await _context.Users.ReplaceOneAsync(u => u.Id == existingUser.Id, existingUser);
                        }
                        else
                        {
                            var shopAdmin = new User
                            {
                                TenantId = newTenant.Id,
                                Username = transaction.CustomerEmail,
                                Email = transaction.CustomerEmail,
                                FullName = newTenant.OwnerName,
                                PhoneNumber = newTenant.Phone,
                                Role = Role.Admin,
                                EmployeeId = "EMP-0001",
                                IsLocked = false,
                                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Welcome@123"),
                                CreatedDate = DateTime.UtcNow,
                                UpdatedDate = DateTime.UtcNow
                            };
                            await _context.Users.InsertOneAsync(shopAdmin);
                        }
                    }
                }

                if (string.IsNullOrEmpty(activeTenantId))
                {
                    throw new InvalidOperationException("Tenant identification is missing for subscription activation.");
                }

                // Renew or extend license for this tenant
                await _licenseService.RenewOrExtendLicenseAsync(activeTenantId, transaction.PackageId, durationDays);

                // Update Tenant status to Active if it was expired or trial
                var targetTenant = await _context.Tenants.Find(t => t.Id == activeTenantId).FirstOrDefaultAsync();
                if (targetTenant != null)
                {
                    targetTenant.Status = "Active";
                    targetTenant.IsPaidCustomer = true;
                    targetTenant.PackageId = transaction.PackageId;
                    targetTenant.UpdatedAt = DateTime.UtcNow;
                    await _context.Tenants.ReplaceOneAsync(t => t.Id == targetTenant.Id, targetTenant);
                }

                // Mark Activated
                transaction.ActivationStatus = "Activated";
                transaction.ActivationDate = DateTime.UtcNow;
                transaction.FailureReason = string.Empty;

                transaction.StateTransitions.Add(new PaymentStateTransition
                {
                    TimestampUtc = DateTime.UtcNow,
                    TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                    FromStatus = "ActivationPending",
                    ToStatus = "Activated",
                    Trigger = "SubscriptionActivated",
                    Details = $"Tenant {activeTenantId} workspace license successfully activated for {durationDays} days."
                });

                // Generate Receipt Number if not set
                if (string.IsNullOrEmpty(transaction.ReceiptNumber))
                {
                    transaction.ReceiptNumber = _receiptService.GenerateReceiptNumber();
                    transaction.ReceiptIssuedAt = DateTime.UtcNow;
                }

                // Generate PDF and Email Receipt
                try
                {
                    var platformSettings = await _platformSettingsService.GetSettingsAsync();
                    var pdfBytes = _receiptService.GeneratePaymentReceiptPdf(transaction, package, platformSettings);
                    transaction.ReceiptGenerated = true;

                    // Send receipt email
                    var recipientEmail = !string.IsNullOrWhiteSpace(transaction.CustomerEmail) 
                        ? transaction.CustomerEmail 
                        : targetTenant?.ContactEmail;

                    if (!string.IsNullOrWhiteSpace(recipientEmail))
                    {
                        var contactName = !string.IsNullOrWhiteSpace(transaction.CustomerName) 
                            ? transaction.CustomerName 
                            : (targetTenant?.OwnerName ?? "Customer");

                        var (emailSuccess, emailErr) = await _emailService.SendPaymentReceiptEmailAsync(
                            recipientEmail,
                            contactName,
                            transaction.PackageName,
                            transaction.ReceiptNumber,
                            $"₹ {transaction.Amount:N2}",
                            !string.IsNullOrEmpty(transaction.RazorpayPaymentId) ? transaction.RazorpayPaymentId : transaction.TransactionRef,
                            pdfBytes);

                        transaction.ReceiptEmailed = emailSuccess;
                        if (!emailSuccess)
                        {
                            transaction.SafeProcessingDetails = $"Receipt email failed: {emailErr}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PAYMENT] Receipt generation or delivery failed for {TxnRef}: {Msg}", transaction.TransactionRef, ex.Message);
                    transaction.SafeProcessingDetails = $"Receipt creation notice: {ex.Message}";
                    // Crucial: Do NOT fail activation because receipt or email failed!
                }

                await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

                await _auditLogService.LogActivityAsync(
                    "SUBSCRIPTION_ACTIVATED_FROM_PAYMENT",
                    transaction.CustomerEmail,
                    $"Tenant: {transaction.TenantId}",
                    $"Activated subscription '{transaction.PackageName}' for {transaction.DurationMonths} month(s) from transaction {transaction.TransactionRef} (Amount: ₹{transaction.Amount:N2})");

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PAYMENT] Subscription activation error for transaction {TxnRef}: {Message}", transaction.TransactionRef, ex.Message);
                transaction.ActivationStatus = "Failed";
                transaction.FailureReason = ex.Message;
                transaction.StateTransitions.Add(new PaymentStateTransition
                {
                    TimestampUtc = DateTime.UtcNow,
                    TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                    FromStatus = "ActivationPending",
                    ToStatus = "Failed",
                    Trigger = "ActivationException",
                    Details = $"Subscription activation failed: {ex.Message}. Preserved in permanent Recovery Queue."
                });
                await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> ProcessWebhookAsync(string rawBody, string signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return (false, "Empty webhook payload.");
            }

            // Verify webhook signature
            bool isSignatureValid = _razorpayService.VerifyWebhookSignature(rawBody, signatureHeader);
            if (!isSignatureValid)
            {
                _logger.LogWarning("[PAYMENT WEBHOOK] Invalid webhook signature received.");
                return (false, "Invalid webhook signature.");
            }

            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                var eventName = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "" : "";
                _logger.LogInformation("[PAYMENT WEBHOOK] Received event: {Event}", eventName);

                string orderId = string.Empty;
                string paymentId = string.Empty;
                string paymentStatus = string.Empty;
                long paymentAmountPaise = 0;
                string paymentCurrency = "INR";
                string txnRefFromNotes = string.Empty;
                string failureReason = string.Empty;

                if (root.TryGetProperty("payload", out var payload))
                {
                    if (payload.TryGetProperty("payment", out var pObj) && pObj.TryGetProperty("entity", out var pEntity))
                    {
                        orderId = pEntity.TryGetProperty("order_id", out var oId) ? oId.GetString() ?? "" : "";
                        paymentId = pEntity.TryGetProperty("id", out var payId) ? payId.GetString() ?? "" : "";
                        paymentStatus = pEntity.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                        paymentCurrency = pEntity.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "INR" : "INR";
                        if (pEntity.TryGetProperty("amount", out var amtProp) && amtProp.TryGetInt64(out var amt))
                        {
                            paymentAmountPaise = amt;
                        }
                        if (pEntity.TryGetProperty("error_description", out var errDesc))
                        {
                            failureReason = errDesc.GetString() ?? "";
                        }
                        if (pEntity.TryGetProperty("notes", out var notesObj) && notesObj.TryGetProperty("txn_ref", out var trProp))
                        {
                            txnRefFromNotes = trProp.GetString() ?? "";
                        }
                    }
                    else if (payload.TryGetProperty("order", out var oObj) && oObj.TryGetProperty("entity", out var oEntity))
                    {
                        orderId = oEntity.TryGetProperty("id", out var oId) ? oId.GetString() ?? "" : "";
                        if (oEntity.TryGetProperty("notes", out var notesObj) && notesObj.TryGetProperty("txn_ref", out var trProp))
                        {
                            txnRefFromNotes = trProp.GetString() ?? "";
                        }
                    }
                }

                // Match transaction by RazorpayOrderId, RazorpayPaymentId, or TransactionRef
                SaaSPaymentTransaction? transaction = null;
                if (!string.IsNullOrEmpty(orderId))
                {
                    transaction = await _context.SaaSPaymentTransactions.Find(t => t.RazorpayOrderId == orderId).FirstOrDefaultAsync();
                }
                if (transaction == null && !string.IsNullOrEmpty(paymentId))
                {
                    transaction = await _context.SaaSPaymentTransactions.Find(t => t.RazorpayPaymentId == paymentId).FirstOrDefaultAsync();
                }
                if (transaction == null && !string.IsNullOrEmpty(txnRefFromNotes))
                {
                    transaction = await _context.SaaSPaymentTransactions.Find(t => t.TransactionRef == txnRefFromNotes).FirstOrDefaultAsync();
                }

                if (transaction == null)
                {
                    _logger.LogWarning("[PAYMENT WEBHOOK] No matching SIMS transaction found for Order: {OrderId}, Payment: {PaymentId}, TxnRef: {TxnRef}", 
                        orderId, paymentId, txnRefFromNotes);
                    return (true, $"Webhook event '{eventName}' received, but no matching transaction found.");
                }

                if (eventName == "payment.captured" || eventName == "order.paid")
                {
                    // If paymentId is empty in order.paid, discover it from Razorpay order payments API
                    if (string.IsNullOrEmpty(paymentId) && !string.IsNullOrEmpty(transaction.RazorpayOrderId))
                    {
                        var (ordSuccess, discPayId, _, _, _, _) = await _razorpayService.FetchOrderPaymentsAsync(transaction.RazorpayOrderId);
                        if (ordSuccess && !string.IsNullOrEmpty(discPayId))
                        {
                            paymentId = discPayId;
                        }
                    }

                    // Security & Financial Integrity Check: Amount and Currency verification
                    if (paymentAmountPaise > 0 && transaction.AmountInPaise > 0)
                    {
                        if (paymentAmountPaise != transaction.AmountInPaise)
                        {
                            _logger.LogWarning("[PAYMENT WEBHOOK] Amount mismatch for {TxnRef}: Expected {Expected} paise, received {Actual} paise.",
                                transaction.TransactionRef, transaction.AmountInPaise, paymentAmountPaise);
                            transaction.SafeProcessingDetails = $"Amount mismatch warning: Webhook reported {paymentAmountPaise} paise vs transaction {transaction.AmountInPaise} paise.";
                            await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);
                            return (false, "Amount mismatch between webhook payload and transaction record.");
                        }
                    }

                    // Auto-restore archived transaction if it was archived by temporary retention timer
                    if (transaction.IsArchived)
                    {
                        transaction.IsArchived = false;
                        transaction.ArchiveReason = string.Empty;
                        transaction.ArchivedAt = null;
                        _logger.LogInformation("[PAYMENT RECOVERY] Restoring previously archived checkout attempt {TxnRef} via late webhook confirmation.", transaction.TransactionRef);
                    }

                    var oldStatus = transaction.PaymentStatus;
                    transaction.PaymentStatus = "Paid";
                    if (!string.IsNullOrEmpty(paymentId))
                    {
                        transaction.RazorpayPaymentId = paymentId;
                    }
                    transaction.VerifiedDate ??= DateTime.UtcNow;
                    transaction.LastReconciliationDate = DateTime.UtcNow;
                    transaction.SafeProcessingDetails = $"Payment captured and confirmed via Razorpay webhook '{eventName}'.";

                    transaction.StateTransitions.Add(new PaymentStateTransition
                    {
                        TimestampUtc = DateTime.UtcNow,
                        TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                        FromStatus = oldStatus,
                        ToStatus = "Paid",
                        Trigger = $"Webhook_{eventName}",
                        Details = $"Razorpay webhook event '{eventName}' confirmed payment. Payment ID: {paymentId}, Order ID: {transaction.RazorpayOrderId}."
                    });

                    await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

                    // Idempotently activate/renew subscription
                    if (transaction.ActivationStatus != "Activated")
                    {
                        await ActivateSubscriptionFromPaymentAsync(transaction.Id);
                    }

                    return (true, $"Processed event {eventName} for order {transaction.RazorpayOrderId}");
                }
                else if (eventName == "payment.failed")
                {
                    if (transaction.PaymentStatus != "Paid")
                    {
                        var oldStatus = transaction.PaymentStatus;
                        transaction.PaymentStatus = "Failed";
                        transaction.FailureReason = !string.IsNullOrWhiteSpace(failureReason) ? failureReason : "Payment failed at Razorpay gateway.";
                        transaction.LastReconciliationDate = DateTime.UtcNow;
                        if (!string.IsNullOrEmpty(paymentId)) transaction.RazorpayPaymentId = paymentId;

                        transaction.StateTransitions.Add(new PaymentStateTransition
                        {
                            TimestampUtc = DateTime.UtcNow,
                            TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                            FromStatus = oldStatus,
                            ToStatus = "Failed",
                            Trigger = "Webhook_payment.failed",
                            Details = $"Payment failed notification from gateway: {transaction.FailureReason}"
                        });

                        await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);
                    }
                    return (true, $"Acknowledged payment failure for {transaction.TransactionRef}");
                }

                return (true, $"Webhook event '{eventName}' acknowledged.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PAYMENT WEBHOOK] Error processing webhook: {Msg}", ex.Message);
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string Message, SaaSPaymentTransaction? Transaction)> ReconcileSingleTransactionAsync(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return (false, "Transaction ID is required for reconciliation.", null);
            }

            try
            {
                var transaction = await _context.SaaSPaymentTransactions
                    .Find(t => t.Id == transactionId || t.TransactionRef == transactionId || t.RazorpayOrderId == transactionId)
                    .FirstOrDefaultAsync();

                if (transaction == null)
                {
                    return (false, "Transaction not found.", null);
                }

                // If already Paid, verify activation status
                if (transaction.PaymentStatus == "Paid")
                {
                    if (transaction.ActivationStatus != "Activated")
                    {
                        var (actSuccess, actError) = await ActivateSubscriptionFromPaymentAsync(transaction.Id);
                        var updatedTx = await _context.SaaSPaymentTransactions.Find(t => t.Id == transaction.Id).FirstOrDefaultAsync();
                        return (true, actSuccess 
                            ? "Payment was already verified. Workspace subscription activated successfully!" 
                            : $"Payment was already verified, but activation notice: {actError}", updatedTx);
                    }
                    return (true, "Transaction is already confirmed and subscription is active.", transaction);
                }

                bool paymentFound = false;
                string capturedPaymentId = string.Empty;
                string gatewayStatus = string.Empty;
                decimal verifiedAmount = 0m;
                string verifiedCurrency = "INR";

                // Strategy 1: Check by Razorpay Order ID (discovers payments even when customer closed browser!)
                if (!string.IsNullOrEmpty(transaction.RazorpayOrderId))
                {
                    var (ordSuccess, payId, status, amountInr, currency, _) = await _razorpayService.FetchOrderPaymentsAsync(transaction.RazorpayOrderId);
                    if (ordSuccess && (status.Equals("captured", StringComparison.OrdinalIgnoreCase) || 
                                       status.Equals("paid", StringComparison.OrdinalIgnoreCase) ||
                                       status.Equals("authorized", StringComparison.OrdinalIgnoreCase)))
                    {
                        paymentFound = true;
                        capturedPaymentId = payId;
                        gatewayStatus = status;
                        verifiedAmount = amountInr;
                        verifiedCurrency = !string.IsNullOrEmpty(currency) ? currency : "INR";
                    }
                }

                // Strategy 2: Check by Razorpay Payment ID if Strategy 1 did not find captured payment
                if (!paymentFound && !string.IsNullOrEmpty(transaction.RazorpayPaymentId))
                {
                    var (paySuccess, status, amountInr, _) = await _razorpayService.FetchPaymentDetailsAsync(transaction.RazorpayPaymentId);
                    if (paySuccess && (status.Equals("captured", StringComparison.OrdinalIgnoreCase) || 
                                       status.Equals("paid", StringComparison.OrdinalIgnoreCase) ||
                                       status.Equals("authorized", StringComparison.OrdinalIgnoreCase)))
                    {
                        paymentFound = true;
                        capturedPaymentId = transaction.RazorpayPaymentId;
                        gatewayStatus = status;
                        verifiedAmount = amountInr;
                    }
                }

                if (paymentFound)
                {
                    // Financial Integrity Check: Ensure verified amount matches transaction
                    if (transaction.Amount > 0 && verifiedAmount > 0 && Math.Abs(transaction.Amount - verifiedAmount) > 0.5m)
                    {
                        _logger.LogWarning("[RECONCILIATION] Amount mismatch on {TxnRef}: Expected ₹{Expected}, Gateway reported ₹{Actual}",
                            transaction.TransactionRef, transaction.Amount, verifiedAmount);
                        transaction.SafeProcessingDetails = $"Amount discrepancy: Gateway reported ₹{verifiedAmount} vs expected ₹{transaction.Amount}.";
                        await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);
                        return (false, $"Amount mismatch: Gateway reported ₹{verifiedAmount} vs expected ₹{transaction.Amount}.", transaction);
                    }

                    // Auto-restore if previously archived by retention
                    if (transaction.IsArchived)
                    {
                        transaction.IsArchived = false;
                        transaction.ArchiveReason = string.Empty;
                        transaction.ArchivedAt = null;
                        _logger.LogInformation("[RECONCILIATION] Restored previously archived checkout attempt {TxnRef} to confirmed revenue.", transaction.TransactionRef);
                    }

                    var oldStatus = transaction.PaymentStatus;
                    transaction.PaymentStatus = "Paid";
                    if (!string.IsNullOrEmpty(capturedPaymentId))
                    {
                        transaction.RazorpayPaymentId = capturedPaymentId;
                    }
                    transaction.VerifiedDate = DateTime.UtcNow;
                    transaction.LastReconciliationDate = DateTime.UtcNow;
                    transaction.SafeProcessingDetails = $"Reconciliation verified against Razorpay ({gatewayStatus}). Payment ID: {capturedPaymentId}.";

                    transaction.StateTransitions.Add(new PaymentStateTransition
                    {
                        TimestampUtc = DateTime.UtcNow,
                        TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                        FromStatus = oldStatus,
                        ToStatus = "Paid",
                        Trigger = "ReconciliationVerified",
                        Details = $"Server-side reconciliation verified payment {capturedPaymentId} as {gatewayStatus} on Razorpay Order {transaction.RazorpayOrderId}."
                    });

                    await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

                    // Idempotently activate subscription
                    var (actSuccess, actError) = await ActivateSubscriptionFromPaymentAsync(transaction.Id);
                    var refreshed = await _context.SaaSPaymentTransactions.Find(t => t.Id == transaction.Id).FirstOrDefaultAsync();

                    string userMessage = actSuccess 
                        ? $"Payment {capturedPaymentId} verified from Razorpay and workspace subscription activated successfully!"
                        : $"Payment {capturedPaymentId} verified as Paid, but activation notice: {actError}";

                    return (true, userMessage, refreshed ?? transaction);
                }
                else
                {
                    // No captured payment was found on Razorpay
                    transaction.ReconciliationAttempts++;
                    transaction.LastReconciliationDate = DateTime.UtcNow;
                    transaction.SafeProcessingDetails = $"Reconciliation checked at {DateTime.UtcNow.AddMinutes(330):dd MMM yyyy, hh:mm:ss tt IST}: No captured payment found on Razorpay.";
                    await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

                    return (false, "Reconciliation check completed: payment is not yet captured or confirmed on Razorpay.", transaction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RECONCILIATION] Error during single transaction reconciliation for {Id}: {Msg}", transactionId, ex.Message);
                return (false, $"Reconciliation error: {ex.Message}", null);
            }
        }

        public async Task<(int ReconciledCount, int ActivatedCount, int FailedCount)> ReconcilePendingPaymentsAsync()
        {
            int reconciledCount = 0;
            int activatedCount = 0;
            int failedCount = 0;

            try
            {
                // 1. Process Paid but unactivated transactions (Permanent Recovery Queue)
                var unactivated = await _context.SaaSPaymentTransactions
                    .Find(t => t.PaymentStatus == "Paid" && t.ActivationStatus != "Activated")
                    .ToListAsync();

                foreach (var tx in unactivated)
                {
                    tx.ReconciliationAttempts++;
                    tx.LastReconciliationDate = DateTime.UtcNow;
                    var (success, err) = await ActivateSubscriptionFromPaymentAsync(tx.Id);
                    if (success)
                    {
                        activatedCount++;
                        reconciledCount++;
                    }
                    else
                    {
                        failedCount++;
                        _logger.LogWarning("[RECONCILIATION] Failed to activate paid transaction {TxnRef}: {Err}", tx.TransactionRef, err);
                    }
                }

                // 2. Process pending checkout attempts within last 7 days that have not been checked recently
                //    Throttled to avoid unnecessary server load and external Razorpay rate limits
                var minRecheckThreshold = DateTime.UtcNow.AddMinutes(-30);
                var pendingCandidates = await _context.SaaSPaymentTransactions
                    .Find(t => t.PaymentStatus == "Pending" 
                            && t.CreatedDate < DateTime.UtcNow.AddMinutes(-2) 
                            && t.CreatedDate > DateTime.UtcNow.AddDays(-7)
                            && (t.LastReconciliationDate == null || t.LastReconciliationDate < minRecheckThreshold)
                            && (t.ReconciliationAttempts < 10)
                            && (!string.IsNullOrEmpty(t.RazorpayOrderId) || !string.IsNullOrEmpty(t.RazorpayPaymentId)))
                    .Limit(25)
                    .ToListAsync();

                foreach (var tx in pendingCandidates)
                {
                    var (recSuccess, _, _) = await ReconcileSingleTransactionAsync(tx.Id);
                    if (recSuccess)
                    {
                        reconciledCount++;
                        var rechecked = await _context.SaaSPaymentTransactions.Find(t => t.Id == tx.Id).FirstOrDefaultAsync();
                        if (rechecked?.ActivationStatus == "Activated")
                        {
                            activatedCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RECONCILIATION] Error during batch payment reconciliation: {Msg}", ex.Message);
            }

            return (reconciledCount, activatedCount, failedCount);
        }

        public async Task<int> CleanupExpiredTemporaryCheckoutsAsync()
        {
            try
            {
                var settings = await _platformSettingsService.GetSettingsAsync();
                var retentionSpan = settings.GetTemporaryCheckoutRetentionTimeSpan();
                var cutoffUtc = DateTime.UtcNow.Subtract(retentionSpan);

                // CRITICAL SAFETY RULES:
                // 1. NEVER delete or archive verified successful payments (PaymentStatus == "Paid")
                // 2. NEVER delete or archive paid-but-unactivated cases (ActivationStatus != "Activated" when Paid)
                // 3. NEVER delete or archive records with unresolved refund/chargeback (RefundPending, Refunded)
                // 4. Only target unconfirmed checkout attempts older than retention policy
                var filterBuilder = Builders<SaaSPaymentTransaction>.Filter;
                var safeArchiveFilter = filterBuilder.And(
                    filterBuilder.Ne(t => t.PaymentStatus, "Paid"),
                    filterBuilder.Ne(t => t.ActivationStatus, "Activated"),
                    filterBuilder.Ne(t => t.PaymentStatus, "RefundPending"),
                    filterBuilder.Ne(t => t.PaymentStatus, "Refunded"),
                    filterBuilder.Ne(t => t.IsArchived, true),
                    filterBuilder.Or(
                        filterBuilder.And(
                            filterBuilder.Ne(t => t.ExpiresAt, null),
                            filterBuilder.Lte(t => t.ExpiresAt, DateTime.UtcNow)
                        ),
                        filterBuilder.Lte(t => t.CreatedDate, cutoffUtc)
                    )
                );

                var expiredCandidates = await _context.SaaSPaymentTransactions
                    .Find(safeArchiveFilter)
                    .Limit(100)
                    .ToListAsync();

                if (!expiredCandidates.Any())
                {
                    return 0;
                }

                int archivedCount = 0;
                var istNow = DateTime.UtcNow.AddMinutes(330);

                foreach (var tx in expiredCandidates)
                {
                    tx.IsArchived = true;
                    tx.ArchivedAt = DateTime.UtcNow;
                    tx.ArchiveReason = $"Temporary checkout retention expired ({settings.TemporaryCheckoutRetentionValue} {settings.TemporaryCheckoutRetentionUnit})";

                    tx.StateTransitions.Add(new PaymentStateTransition
                    {
                        TimestampUtc = DateTime.UtcNow,
                        TimestampIst = istNow.ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                        FromStatus = tx.PaymentStatus,
                        ToStatus = tx.PaymentStatus,
                        Trigger = "RetentionArchived",
                        Details = $"Moved to archived temporary checkouts. Retention period of {settings.TemporaryCheckoutRetentionValue} {settings.TemporaryCheckoutRetentionUnit} elapsed without payment confirmation."
                    });

                    await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == tx.Id, tx);
                    archivedCount++;
                }

                _logger.LogInformation("[RETENTION CLEANUP] Safely archived {Count} expired temporary checkout attempts older than {Cutoff:yyyy-MM-dd HH:mm:ss UTC}.", 
                    archivedCount, cutoffUtc);

                return archivedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RETENTION CLEANUP] Error running temporary checkout cleanup: {Msg}", ex.Message);
                return 0;
            }
        }

        public async Task<(bool Success, string Message)> RecordCustomerCheckoutCancellationAsync(string orderIdOrTxnRef, string? reason = null)
        {
            if (string.IsNullOrWhiteSpace(orderIdOrTxnRef))
            {
                return (false, "Order ID or Transaction Reference is required.");
            }

            var tx = await _context.SaaSPaymentTransactions
                .Find(t => t.RazorpayOrderId == orderIdOrTxnRef || t.TransactionRef == orderIdOrTxnRef)
                .FirstOrDefaultAsync();

            if (tx == null)
            {
                return (false, "Transaction not found.");
            }

            // Do not alter confirmed paid payments
            if (tx.PaymentStatus == "Paid")
            {
                return (true, "Payment already confirmed.");
            }

            tx.IsCustomerCancelled = true;
            tx.CustomerCancelledAt = DateTime.UtcNow;

            tx.StateTransitions.Add(new PaymentStateTransition
            {
                TimestampUtc = DateTime.UtcNow,
                TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                FromStatus = tx.PaymentStatus,
                ToStatus = tx.PaymentStatus,
                Trigger = "CustomerDismissedCheckout",
                Details = !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : "Customer closed or dismissed Razorpay Checkout modal."
            });

            await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == tx.Id, tx);
            return (true, "Cancellation recorded.");
        }

        public async Task<(bool Success, string Message)> ResolvePaymentCaseManuallyAsync(string transactionId, string resolutionNotes, string adminUser)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return (false, "Transaction ID is required.");
            }

            var tx = await _context.SaaSPaymentTransactions.Find(t => t.Id == transactionId).FirstOrDefaultAsync();
            if (tx == null)
            {
                return (false, "Transaction not found.");
            }

            tx.IsManuallyResolved = true;
            tx.ResolutionNotes = !string.IsNullOrWhiteSpace(resolutionNotes) ? resolutionNotes.Trim() : "Resolved manually by Super Admin.";
            tx.ResolvedAt = DateTime.UtcNow;
            tx.ResolvedBy = !string.IsNullOrWhiteSpace(adminUser) ? adminUser.Trim() : "SuperAdmin";

            tx.StateTransitions.Add(new PaymentStateTransition
            {
                TimestampUtc = DateTime.UtcNow,
                TimestampIst = DateTime.UtcNow.AddMinutes(330).ToString("dd MMM yyyy, hh:mm:ss tt IST"),
                FromStatus = tx.ActivationStatus,
                ToStatus = "ResolvedManually",
                Trigger = "AdminManualResolution",
                Details = $"Recovery case marked resolved by {tx.ResolvedBy}. Notes: {tx.ResolutionNotes}"
            });

            await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == tx.Id, tx);

            await _auditLogService.LogActivityAsync(
                "PAYMENT_CASE_MANUALLY_RESOLVED",
                tx.CustomerEmail,
                $"TxnRef: {tx.TransactionRef}",
                $"Payment recovery case resolved manually by {tx.ResolvedBy}. Notes: {tx.ResolutionNotes}");

            return (true, "Payment recovery case marked resolved successfully.");
        }

        public async Task<(bool Success, string ErrorMessage)> RetryReceiptEmailAsync(string transactionId)
        {
            var transaction = await _context.SaaSPaymentTransactions.Find(t => t.Id == transactionId).FirstOrDefaultAsync();
            if (transaction == null) return (false, "Transaction not found.");

            if (transaction.PaymentStatus != "Paid")
            {
                return (false, "Cannot send receipt for unpaid transaction.");
            }

            if (string.IsNullOrEmpty(transaction.ReceiptNumber))
            {
                transaction.ReceiptNumber = _receiptService.GenerateReceiptNumber();
                transaction.ReceiptIssuedAt = DateTime.UtcNow;
            }

            var package = await _context.SubscriptionPackages.Find(p => p.Id == transaction.PackageId).FirstOrDefaultAsync();
            var platformSettings = await _platformSettingsService.GetSettingsAsync();
            var pdfBytes = _receiptService.GeneratePaymentReceiptPdf(transaction, package, platformSettings);

            var (emailSuccess, emailErr) = await _emailService.SendPaymentReceiptEmailAsync(
                transaction.CustomerEmail,
                transaction.CustomerName,
                transaction.PackageName,
                transaction.ReceiptNumber,
                $"₹ {transaction.Amount:N2}",
                !string.IsNullOrEmpty(transaction.RazorpayPaymentId) ? transaction.RazorpayPaymentId : transaction.TransactionRef,
                pdfBytes);

            transaction.ReceiptGenerated = true;
            transaction.ReceiptEmailed = emailSuccess;
            await _context.SaaSPaymentTransactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);

            return (emailSuccess, emailErr ?? string.Empty);
        }

        public async Task<RevenueMetricsViewModel> GetRevenueMetricsAsync()
        {
            var allTransactions = await _context.SaaSPaymentTransactions
                .Find(_ => true)
                .SortByDescending(t => t.CreatedDate)
                .ToListAsync();

            // ONLY verified successful payments count as confirmed revenue!
            var verifiedPaid = allTransactions.Where(t => t.PaymentStatus == "Paid").ToList();

            var nowIst = DateTime.UtcNow.AddMinutes(330);
            var todayIstDate = nowIst.Date;
            var currentMonth = nowIst.Month;
            var currentYear = nowIst.Year;

            decimal totalVerifiedRevenue = verifiedPaid.Sum(t => t.Amount);

            decimal todayRevenue = verifiedPaid
                .Where(t => (t.VerifiedDate ?? t.CreatedDate).AddMinutes(330).Date == todayIstDate)
                .Sum(t => t.Amount);

            decimal currentMonthRevenue = verifiedPaid
                .Where(t =>
                    (t.VerifiedDate ?? t.CreatedDate).AddMinutes(330).Year == currentYear &&
                    (t.VerifiedDate ?? t.CreatedDate).AddMinutes(330).Month == currentMonth)
                .Sum(t => t.Amount);

            decimal currentYearRevenue = verifiedPaid
                .Where(t => (t.VerifiedDate ?? t.CreatedDate).AddMinutes(330).Year == currentYear)
                .Sum(t => t.Amount);

            // Paid-but-unactivated recovery queue (Permanent: never deleted, never expired)
            var unactivated = verifiedPaid
                .Where(t => t.ActivationStatus != "Activated" && !t.IsManuallyResolved)
                .ToList();

            var platformSettings = await _platformSettingsService.GetSettingsAsync();
            var retentionSpan = platformSettings.GetTemporaryCheckoutRetentionTimeSpan();
            var cutoffUtc = DateTime.UtcNow.Subtract(retentionSpan);

            // Recent active checkout attempts (strictly unconfirmed, not archived, and within active retention window)
            var recentCheckouts = allTransactions
                .Where(t => t.PaymentStatus != "Paid" && !t.IsArchived && t.CreatedDate >= cutoffUtc)
                .ToList();

            // Archived / Expired checkout attempts (explicitly archived or unconfirmed checkouts older than retention policy)
            var archivedAttempts = allTransactions
                .Where(t => t.IsArchived || (t.PaymentStatus != "Paid" && t.CreatedDate < cutoffUtc))
                .ToList();

            return new RevenueMetricsViewModel
            {
                TotalVerifiedRevenue = totalVerifiedRevenue,
                TodayRevenue = todayRevenue,
                CurrentMonthRevenue = currentMonthRevenue,
                CurrentYearRevenue = currentYearRevenue,
                SuccessfulPaymentCount = verifiedPaid.Count,
                PendingPaymentCount = recentCheckouts.Count,
                FailedPaymentCount = allTransactions.Count(t => t.PaymentStatus == "Failed" || t.PaymentStatus == "Cancelled"),
                AwaitingActivationCount = unactivated.Count,
                RecentCheckoutsCount = recentCheckouts.Count,
                ArchivedAttemptsCount = archivedAttempts.Count,
                RefundDisputeCount = allTransactions.Count(t => t.PaymentStatus == "RefundPending" || t.PaymentStatus == "Refunded"),
                RefundTotal = allTransactions.Where(t => t.PaymentStatus == "Refunded").Sum(t => t.Amount),
                RecentTransactions = allTransactions.Where(t => !t.IsArchived).Take(100).ToList(),
                UnactivatedTransactions = unactivated,
                ArchivedTransactions = archivedAttempts.Take(100).ToList()
            };
        }
    }
}
