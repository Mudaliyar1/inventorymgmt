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
        Task SendPasswordResetEmailAsync(string toEmail, string recipientName, string resetLink, string accountType);
        Task SendPasswordResetOtpEmailAsync(string toEmail, string otpCode, string recipientName);
        Task SendPasswordChangedEmailAsync(string toEmail, string username);
        Task SendLowStockAlertEmailAsync(string toEmail, string productName, int currentStock, int minStock);
        Task SendNewUserCreatedEmailAsync(string toEmail, string username, string role, string tempPassword);
        Task SendLicenseRenewalApprovedEmailAsync(string toEmail, string shopName, string ownerName, string packageName, int extensionDays, System.DateTime newExpiryDate, string? adminNotes);
        Task SendLicenseRenewalRejectedEmailAsync(string toEmail, string shopName, string ownerName, string packageName, string? rejectionReason);
        Task SendShopSuspendedEmailAsync(string toEmail, string shopName, string ownerName, string? reason = null);
        Task SendLicenseExpiredEmailAsync(string toEmail, string shopName, string ownerName, System.DateTime expiryDate);

        // Transactional Email with Result (Diagnostics & Reliable Status Tracking)
        Task<(bool Success, string? ErrorMessage)> SendEmailWithResultAsync(string toEmail, string subject, string htmlMessage);
        Task<(bool Success, string? ErrorMessage)> SendCustomQuoteConfirmationEmailAsync(string toEmail, string contactName, string inquiryReference);
        Task<(bool Success, string? ErrorMessage)> SendContactInquiryConfirmationEmailAsync(string toEmail, string customerName, string inquiryReference);
        Task<(bool Success, string? ErrorMessage)> SendCustomQuoteReplyEmailAsync(string toEmail, string contactName, string inquiryReference, string status, string message);
        Task<(bool Success, string? ErrorMessage)> SendContactInquiryReplyEmailAsync(string toEmail, string customerName, string inquiryReference, string status, string message);

        // Free Trial & Subscription Notifications
        Task<(bool Success, string? ErrorMessage)> SendRegistrationOtpEmailAsync(string toEmail, string contactName, string otp, int expiryMinutes);
        Task<(bool Success, string? ErrorMessage)> SendTrialRegistrationPendingEmailAsync(string toEmail, string contactName);
        Task<(bool Success, string? ErrorMessage)> SendTrialApprovedEmailAsync(string toEmail, string contactName, System.DateTime trialStartUtc, System.DateTime trialExpiryUtc);
        Task<(bool Success, string? ErrorMessage)> SendTrialRejectedEmailAsync(string toEmail, string contactName, string? rejectionReason);
        Task<(bool Success, string? ErrorMessage)> SendPaymentReceiptEmailAsync(string toEmail, string contactName, string packageName, string receiptNumber, string amountPaidFormatted, string paymentReference, byte[] receiptPdfBytes);
        Task<(bool Success, string? ErrorMessage)> SendTrialReminderEmailAsync(string toEmail, string contactName, System.DateTime expiryUtc, string reminderType, string pricingUrl);
        Task<(bool Success, string? ErrorMessage)> SendTrialExpiredEmailAsync(string toEmail, string contactName, System.DateTime expiryUtc, string pricingUrl);
        Task<(bool Success, string? ErrorMessage)> SendSubscriptionReminderEmailAsync(string toEmail, string contactName, System.DateTime expiryUtc, string reminderType, string renewalUrl);
        Task<(bool Success, string? ErrorMessage)> SendSubscriptionExpiredEmailAsync(string toEmail, string contactName, System.DateTime expiryUtc, string pricingUrl);
    }
}
