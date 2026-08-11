using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class InventoryAlertService : IInventoryAlertService
    {
        private readonly IInventoryAlertRepository _alertRepository;
        private readonly IBrevoEmailService _brevoEmailService;
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<InventoryAlertService> _logger;

        public InventoryAlertService(
            IInventoryAlertRepository alertRepository,
            IBrevoEmailService brevoEmailService,
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IUserRepository userRepository,
            ILogger<InventoryAlertService> logger)
        {
            _alertRepository = alertRepository;
            _brevoEmailService = brevoEmailService;
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task CheckAndTriggerStockAlertsAsync(Product product, int previousStock, int newStock)
        {
            if (product == null) return;

            // Fire and forget asynchronously in background so inventory operations never block
            _ = Task.Run(async () =>
            {
                try
                {
                    var settings = await _alertRepository.GetSettingsAsync();
                    int threshold = product.MinimumStock > 0 ? product.MinimumStock : settings.LowStockThreshold;

                    string alertTypeToTrigger = "";
                    string lastState = product.LastAlertSentType ?? "None";

                    if (newStock == 0 && lastState != "OutOfStock" && settings.EnableOutOfStockAlerts)
                    {
                        alertTypeToTrigger = "OutOfStock";
                    }
                    else if (newStock > 0 && newStock <= threshold && lastState != "LowStock" && lastState != "OutOfStock" && settings.EnableLowStockAlerts)
                    {
                        alertTypeToTrigger = "LowStock";
                    }
                    else if (newStock > 0 && (previousStock == 0 || lastState == "OutOfStock" || lastState == "LowStock") && newStock > threshold && settings.EnableStockRestoredAlerts)
                    {
                        alertTypeToTrigger = "StockRestored";
                    }

                    if (string.IsNullOrEmpty(alertTypeToTrigger)) return;

                    var categoryName = "General";
                    if (!string.IsNullOrEmpty(product.CategoryId))
                    {
                        var cat = await _categoryRepository.GetByIdAsync(product.CategoryId);
                        if (cat != null) categoryName = cat.Name;
                    }

                    var recipients = new List<string>();

                    // 1. Admin Email from Alert Policy Settings
                    if (!string.IsNullOrWhiteSpace(settings.AdminEmail)) 
                        recipients.Add(settings.AdminEmail.Trim());

                    // 2. Custom Alert Recipients from Settings
                    if (settings.AlertRecipients != null)
                    {
                        recipients.AddRange(settings.AlertRecipients.Where(r => !string.IsNullOrWhiteSpace(r)));
                    }

                    // 3. All Active Admin and Employee User Email Addresses from MongoDB
                    try
                    {
                        var allUsers = await _userRepository.GetAllAsync();
                        var activeUserEmails = allUsers
                            .Where(u => !u.IsLocked && !string.IsNullOrWhiteSpace(u.Email))
                            .Select(u => u.Email.Trim());
                        recipients.AddRange(activeUserEmails);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not fetch active system user email addresses for inventory alert.");
                    }

                    recipients = recipients.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    if (!recipients.Any()) return;

                    var primaryRecipient = recipients.First();
                    var ccRecipients = recipients.Skip(1).ToList();

                    string subject = "";
                    string htmlBody = "";
                    string alertColor = "#3B82F6";

                    var istTimeStr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")).ToString("MMM dd, yyyy HH:mm:ss") + " IST";

                    if (alertTypeToTrigger == "OutOfStock")
                    {
                        subject = $"URGENT: Product Out of Stock - {product.Name}";
                        alertColor = "#EF4444";
                        htmlBody = BuildAlertHtml(
                            title: "CRITICAL: Product Out of Stock",
                            product: product,
                            categoryName: categoryName,
                            statusText: "OUT OF STOCK",
                            badgeColor: alertColor,
                            detailsHtml: $"<p style='margin:4px 0;'><strong>Current Stock:</strong> <span style='color:#EF4444;font-weight:bold;'>0 units</span></p>" +
                                         $"<p style='margin:4px 0;'><strong>Configured Threshold:</strong> {threshold} units</p>" +
                                         $"<p style='margin:4px 0;'><strong>Previous Stock:</strong> {previousStock} units</p>",
                            actionRequired: "Immediate stock replenishment required to avoid missing customer orders.",
                            istTimeStr: istTimeStr
                        );
                    }
                    else if (alertTypeToTrigger == "LowStock")
                    {
                        subject = $"LOW STOCK WARNING: {product.Name} ({newStock} units remaining)";
                        alertColor = "#F59E0B";
                        htmlBody = BuildAlertHtml(
                            title: "WARNING: Low Stock Threshold Reached",
                            product: product,
                            categoryName: categoryName,
                            statusText: "LOW STOCK",
                            badgeColor: alertColor,
                            detailsHtml: $"<p style='margin:4px 0;'><strong>Current Stock:</strong> <span style='color:#F59E0B;font-weight:bold;'>{newStock} units</span></p>" +
                                         $"<p style='margin:4px 0;'><strong>Minimum Threshold:</strong> {threshold} units</p>",
                            actionRequired: "Consider reordering this item soon to maintain optimal inventory levels.",
                            istTimeStr: istTimeStr
                        );
                    }
                    else if (alertTypeToTrigger == "StockRestored")
                    {
                        subject = $"STOCK RESTORED: {product.Name} ({newStock} units available)";
                        alertColor = "#22C55E";
                        htmlBody = BuildAlertHtml(
                            title: "SUCCESS: Inventory Stock Restored",
                            product: product,
                            categoryName: categoryName,
                            statusText: "STOCK RESTORED",
                            badgeColor: alertColor,
                            detailsHtml: $"<p style='margin:4px 0;'><strong>New Stock:</strong> <span style='color:#22C55E;font-weight:bold;'>{newStock} units</span></p>" +
                                         $"<p style='margin:4px 0;'><strong>Restocked Quantity:</strong> +{newStock - previousStock} units</p>" +
                                         $"<p style='margin:4px 0;'><strong>Previous Stock:</strong> {previousStock} units</p>",
                            actionRequired: "Item is now available for sales transactions and billing.",
                            istTimeStr: istTimeStr
                        );
                    }

                    var sendResult = await _brevoEmailService.SendTransactionalEmailAsync(primaryRecipient, subject, htmlBody, ccRecipients);

                    var emailLog = new InventoryEmailLog
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Sku = product.Code,
                        CategoryName = categoryName,
                        AlertType = alertTypeToTrigger,
                        RecipientEmail = primaryRecipient,
                        Subject = subject,
                        Status = sendResult.Success ? "Sent" : "Failed",
                        SentAt = DateTime.UtcNow,
                        MessageId = sendResult.MessageId,
                        ApiResponse = sendResult.ApiResponse,
                        ErrorMessage = sendResult.ErrorMessage,
                        RetryCount = sendResult.Success ? 1 : 3
                    };

                    await _alertRepository.CreateEmailLogAsync(emailLog);

                    // Update product alert state to prevent duplicate emails
                    product.LastAlertSentType = alertTypeToTrigger == "StockRestored" ? "None" : alertTypeToTrigger;
                    product.UpdatedDate = DateTime.UtcNow;
                    await _productRepository.UpdateAsync(product.Id, product);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing inventory stock alert for product {ProductId}", product.Id);
                }
            });

            await Task.CompletedTask;
        }

        public async Task<(bool Success, string Message)> SendTestEmailAsync(string recipientEmail)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail)) return (false, "Recipient email address is required.");

            var istTimeStr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")).ToString("MMM dd, yyyy HH:mm:ss") + " IST";

            string subject = "SIMS Inventory Alert System - Brevo API Connection Test";
            string htmlContent = $@"
            <div style='font-family: Arial, sans-serif; background-color: #0F172A; color: #F8FAFC; padding: 30px; border-radius: 10px;'>
                <h2 style='color: #3B82F6; margin-top: 0;'>SIMS Inventory System</h2>
                <div style='background-color: #1E293B; border-left: 4px solid #3B82F6; padding: 20px; border-radius: 6px; margin: 20px 0;'>
                    <h3 style='color: #22C55E; margin-top: 0;'>Brevo API Connection Verified!</h3>
                    <p style='color: #CBD5E1;'>This test email confirms that your Brevo API integration, sender configuration, and notification pipelines are operating successfully.</p>
                    <p style='color: #94A3B8; font-size: 13px;'>Timestamp: {istTimeStr}</p>
                </div>
                <p style='color: #64748B; font-size: 12px;'>Smart Inventory Management System Audit Engine</p>
            </div>";

            var result = await _brevoEmailService.SendTransactionalEmailAsync(recipientEmail.Trim(), subject, htmlContent);

            var log = new InventoryEmailLog
            {
                ProductId = "SYSTEM",
                ProductName = "Brevo API Test",
                Sku = "TEST-001",
                CategoryName = "System",
                AlertType = "TestEmail",
                RecipientEmail = recipientEmail.Trim(),
                Subject = subject,
                Status = result.Success ? "Sent" : "Failed",
                SentAt = DateTime.UtcNow,
                MessageId = result.MessageId,
                ApiResponse = result.ApiResponse,
                ErrorMessage = result.ErrorMessage,
                RetryCount = result.Success ? 1 : 3
            };
            await _alertRepository.CreateEmailLogAsync(log);

            if (result.Success)
            {
                return (true, $"Test email sent successfully to {recipientEmail}. Message ID: {result.MessageId}");
            }
            else
            {
                return (false, $"Failed to send test email via Brevo API: {result.ErrorMessage}");
            }
        }

        public async Task<InventoryAlertSettings> GetSettingsAsync()
        {
            return await _alertRepository.GetSettingsAsync();
        }

        public async Task SaveSettingsAsync(InventoryAlertSettings settings)
        {
            await _alertRepository.SaveSettingsAsync(settings);
        }

        public async Task<(IEnumerable<InventoryEmailLog> Items, long TotalCount)> GetFilteredLogsAsync(
            string? keyword,
            string? alertType,
            string? status,
            int page = 1,
            int pageSize = 20)
        {
            return await _alertRepository.GetFilteredLogsAsync(keyword, alertType, status, page, pageSize);
        }

        public async Task<InventoryAlertDashboardStats> GetDashboardStatsAsync()
        {
            return await _alertRepository.GetDashboardStatsAsync();
        }

        public async Task<(bool Success, string Message)> ResendEmailLogAsync(string logId)
        {
            var log = await _alertRepository.GetEmailLogByIdAsync(logId);
            if (log == null) return (false, "Email log record not found.");

            var result = await _brevoEmailService.SendTransactionalEmailAsync(log.RecipientEmail, log.Subject, $"<div style='font-family:Arial;padding:20px;'><h3>Resent Alert: {log.Subject}</h3><p>Product: {log.ProductName} (SKU: {log.Sku})</p><p>Category: {log.CategoryName}</p></div>");

            log.RetryCount++;
            log.SentAt = DateTime.UtcNow;
            if (result.Success)
            {
                log.Status = "Sent";
                log.MessageId = result.MessageId;
                log.ErrorMessage = string.Empty;
            }
            else
            {
                log.ErrorMessage = result.ErrorMessage;
            }

            await _alertRepository.UpdateEmailLogAsync(log);
            return (result.Success, result.Success ? "Email resent successfully." : $"Failed to resend: {result.ErrorMessage}");
        }

        private static string BuildAlertHtml(
            string title,
            Product product,
            string categoryName,
            string statusText,
            string badgeColor,
            string detailsHtml,
            string actionRequired,
            string istTimeStr)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>
                    body {{ font-family: 'Inter', -apple-system, BlinkMacSystemFont, Arial, sans-serif; background-color: #0F172A; color: #F8FAFC; margin: 0; padding: 20px; }}
                    .container {{ max-width: 600px; margin: 0 auto; background: #1E293B; border: 1px solid #334155; border-radius: 12px; overflow: hidden; }}
                    .header {{ background: #111827; padding: 24px; border-bottom: 1px solid #334155; display: flex; align-items: center; justify-content: space-between; }}
                    .brand {{ font-size: 18px; font-weight: bold; color: #3B82F6; letter-spacing: -0.5px; }}
                    .badge {{ background-color: {badgeColor}; color: #FFFFFF; font-size: 12px; font-weight: bold; padding: 6px 12px; border-radius: 20px; text-transform: uppercase; letter-spacing: 0.5px; }}
                    .body {{ padding: 28px; }}
                    .title {{ font-size: 20px; font-weight: bold; color: #F8FAFC; margin-top: 0; margin-bottom: 16px; }}
                    .product-box {{ background: #0F172A; border: 1px solid #334155; border-radius: 8px; padding: 18px; margin-bottom: 20px; }}
                    .p-name {{ font-size: 16px; font-weight: bold; color: #38BDF8; margin-bottom: 6px; }}
                    .p-meta {{ font-size: 13px; color: #94A3B8; margin-bottom: 12px; }}
                    .action-box {{ background: rgba(59,130,246,0.1); border-left: 4px solid #3B82F6; padding: 14px; border-radius: 4px; font-size: 13px; color: #CBD5E1; margin-bottom: 20px; }}
                    .footer {{ background: #111827; padding: 16px 28px; border-top: 1px solid #334155; font-size: 12px; color: #64748B; text-align: center; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <div class='brand'>SIMS Inventory System</div>
                        <span class='badge'>{statusText}</span>
                    </div>
                    <div class='body'>
                        <div class='title'>{title}</div>
                        <div class='product-box'>
                            <div class='p-name'>{product.Name}</div>
                            <div class='p-meta'>SKU: <strong>{product.Code}</strong> &bull; Category: <strong>{categoryName}</strong> &bull; Selling Price: &#8377;{product.SellingPrice:N2}</div>
                            <hr style='border:0; border-top:1px solid #334155; margin:12px 0;'>
                            {detailsHtml}
                            <p style='margin:8px 0 0 0; font-size:12px; color:#94A3B8;'>Updated: {istTimeStr}</p>
                        </div>
                        <div class='action-box'>
                            <strong>Action Required:</strong> {actionRequired}
                        </div>
                    </div>
                    <div class='footer'>
                        Smart Inventory Management System &bull; Enterprise Real-time Alert Engine
                    </div>
                </div>
            </body>
            </html>";
        }
    }
}
