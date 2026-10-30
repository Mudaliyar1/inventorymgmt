using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class SubscriptionLifecycleBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionLifecycleBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);
        private DateTime _lastReconciliationUtc = DateTime.MinValue;

        public SubscriptionLifecycleBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<SubscriptionLifecycleBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[LIFECYCLE] Subscription Lifecycle Background Service started.");

            // Brief initial delay to let application start up completely
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSubscriptionLifecyclesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[LIFECYCLE] Error running subscription lifecycle processor: {Message}", ex.Message);
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("[LIFECYCLE] Subscription Lifecycle Background Service stopping.");
        }

        private async Task ProcessSubscriptionLifecyclesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var settingsService = scope.ServiceProvider.GetService<IPlatformSettingsService>();
            var saasPaymentService = scope.ServiceProvider.GetService<ISaaSPaymentService>();

            // Run automated payment reconciliation and retention cleanup every 15 minutes (to keep server load low)
            if (saasPaymentService != null && (DateTime.UtcNow - _lastReconciliationUtc) >= TimeSpan.FromMinutes(15))
            {
                _lastReconciliationUtc = DateTime.UtcNow;
                try
                {
                    var (reconciled, activated, failed) = await saasPaymentService.ReconcilePendingPaymentsAsync();
                    if (reconciled > 0 || activated > 0)
                    {
                        _logger.LogInformation("[RECONCILIATION] Periodic background check: {Rec} reconciled, {Act} activated.", reconciled, activated);
                    }
                    await saasPaymentService.CleanupExpiredTemporaryCheckoutsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[PAYMENT RECONCILIATION & RETENTION] Background check notice: {Message}", ex.Message);
                }
            }

            string baseUrl = "https://sims-saas.com";
            if (settingsService != null)
            {
                try
                {
                    var settings = await settingsService.GetSettingsAsync();
                    if (!string.IsNullOrWhiteSpace(settings.SystemUrl))
                    {
                        baseUrl = settings.SystemUrl.TrimEnd('/');
                    }
                }
                catch { }
            }

            string pricingUrl = $"{baseUrl}/Landing/Pricing";
            string renewalUrl = $"{baseUrl}/Subscription/Renew";

            var nowUtc = DateTime.UtcNow;

            // 1. Fetch non-cancelled, non-superseded licenses that are either Active, Trial, ExpiringSoon, or Expired without email sent
            var activeLicenses = await context.TenantLicenses
                .Find(l => l.Status == "Active" || l.Status == "Trial" || l.Status == "ExpiringSoon" || (l.Status == "Expired" && !l.ExpirationEmailSent))
                .ToListAsync();

            foreach (var license in activeLicenses)
            {
                try
                {
                    var tenant = await context.Tenants.Find(t => t.Id == license.TenantId).FirstOrDefaultAsync();
                    if (tenant == null) continue;

                    string contactEmail = tenant.ContactEmail;
                    string contactName = !string.IsNullOrWhiteSpace(tenant.OwnerName) ? tenant.OwnerName : tenant.ShopName;

                    // Fallback to primary tenant admin user if contact email is missing
                    if (string.IsNullOrWhiteSpace(contactEmail))
                    {
                        var adminUser = await context.Users
                            .Find(u => u.TenantId == tenant.Id && (u.Role == "ShopAdmin" || u.Role == "Admin"))
                            .FirstOrDefaultAsync();
                        if (adminUser != null && !string.IsNullOrWhiteSpace(adminUser.Email))
                        {
                            contactEmail = adminUser.Email;
                            if (string.IsNullOrWhiteSpace(contactName))
                            {
                                contactName = !string.IsNullOrWhiteSpace(adminUser.FullName) ? adminUser.FullName : adminUser.Username;
                            }
                        }
                    }

                    var pkg = !string.IsNullOrEmpty(license.PackageId)
                        ? await context.SubscriptionPackages.Find(p => p.Id == license.PackageId).FirstOrDefaultAsync()
                        : null;

                    bool isTrial = license.Status == "Trial" || (pkg != null && pkg.MonthlyPrice == 0 && string.IsNullOrEmpty(license.LastPaymentId));
                    var timeUntilExpiry = license.ExpiryDate - nowUtc;

                    // CASE A: TRIAL SUBSCRIPTIONS
                    if (isTrial)
                    {
                        // Check if trial has expired
                        if (timeUntilExpiry <= TimeSpan.Zero)
                        {
                            // 1. Mark as expired in DB
                            license.Status = "Expired";
                            license.UpdatedAt = nowUtc;

                            tenant.Status = "Expired";
                            tenant.UpdatedAt = nowUtc;
                            await context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);

                            // 2. Check if notification already logged
                            bool alreadySent = await context.SubscriptionNotificationLogs
                                .Find(n => n.LicenseId == license.Id && n.NotificationType == "TrialExpired" && n.Status == "Sent")
                                .AnyAsync();

                            if (!alreadySent && !string.IsNullOrWhiteSpace(contactEmail))
                            {
                                var (success, err) = await emailService.SendTrialExpiredEmailAsync(contactEmail, contactName, license.ExpiryDate, pricingUrl);
                                await LogNotificationAsync(context, tenant.Id, license.Id, "TrialExpired", contactEmail, nowUtc, success, err);
                                _logger.LogInformation("[LIFECYCLE] Trial expired email dispatched to {Email} for Tenant {TenantCode}", contactEmail, tenant.TenantCode);

                                if (success)
                                {
                                    license.ExpirationEmailSent = true;
                                }
                            }
                            else if (alreadySent)
                            {
                                license.ExpirationEmailSent = true;
                            }

                            await context.TenantLicenses.ReplaceOneAsync(l => l.Id == license.Id, license);
                            continue;
                        }

                        // Check Trial Reminders:
                        var totalDuration = license.ExpiryDate - license.StartDate;

                        // 24-Hour Reminder (Only if duration >= 24h)
                        if (totalDuration >= TimeSpan.FromHours(24) && timeUntilExpiry <= TimeSpan.FromHours(24) && timeUntilExpiry > TimeSpan.FromHours(1))
                        {
                            bool alreadySent = await context.SubscriptionNotificationLogs
                                .Find(n => n.LicenseId == license.Id && n.NotificationType == "Trial24HourReminder" && n.Status == "Sent")
                                .AnyAsync();

                            if (!alreadySent && !string.IsNullOrWhiteSpace(contactEmail))
                            {
                                var (success, err) = await emailService.SendTrialReminderEmailAsync(contactEmail, contactName, license.ExpiryDate, "Trial24HourReminder", pricingUrl);
                                await LogNotificationAsync(context, tenant.Id, license.Id, "Trial24HourReminder", contactEmail, nowUtc, success, err);
                                _logger.LogInformation("[LIFECYCLE] 24-Hour trial reminder sent to {Email} for Tenant {TenantCode}", contactEmail, tenant.TenantCode);
                            }
                        }

                        // 1-Hour Reminder (Only if duration >= 1h)
                        if (totalDuration >= TimeSpan.FromHours(1) && timeUntilExpiry <= TimeSpan.FromHours(1) && timeUntilExpiry > TimeSpan.Zero)
                        {
                            bool alreadySent = await context.SubscriptionNotificationLogs
                                .Find(n => n.LicenseId == license.Id && n.NotificationType == "Trial1HourReminder" && n.Status == "Sent")
                                .AnyAsync();

                            if (!alreadySent && !string.IsNullOrWhiteSpace(contactEmail))
                            {
                                var (success, err) = await emailService.SendTrialReminderEmailAsync(contactEmail, contactName, license.ExpiryDate, "Trial1HourReminder", pricingUrl);
                                await LogNotificationAsync(context, tenant.Id, license.Id, "Trial1HourReminder", contactEmail, nowUtc, success, err);
                                _logger.LogInformation("[LIFECYCLE] 1-Hour trial reminder sent to {Email} for Tenant {TenantCode}", contactEmail, tenant.TenantCode);
                            }
                        }
                    }
                    // CASE B: PAID SUBSCRIPTIONS
                    else
                    {
                        // Check if paid subscription has expired
                        if (timeUntilExpiry <= TimeSpan.Zero)
                        {
                            // 1. Mark as expired in DB
                            license.Status = "Expired";
                            license.UpdatedAt = nowUtc;

                            tenant.Status = "Expired";
                            tenant.UpdatedAt = nowUtc;
                            await context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);

                            // 2. Check if notification already logged
                            bool alreadySent = await context.SubscriptionNotificationLogs
                                .Find(n => n.LicenseId == license.Id && n.NotificationType == "SubscriptionExpired" && n.Status == "Sent")
                                .AnyAsync();

                            if (!alreadySent && !string.IsNullOrWhiteSpace(contactEmail))
                            {
                                var (success, err) = await emailService.SendSubscriptionExpiredEmailAsync(contactEmail, contactName, license.ExpiryDate, pricingUrl);
                                await LogNotificationAsync(context, tenant.Id, license.Id, "SubscriptionExpired", contactEmail, nowUtc, success, err);
                                _logger.LogInformation("[LIFECYCLE] Subscription expired email dispatched to {Email} for Tenant {TenantCode}", contactEmail, tenant.TenantCode);

                                if (success)
                                {
                                    license.ExpirationEmailSent = true;
                                }
                            }
                            else if (alreadySent)
                            {
                                license.ExpirationEmailSent = true;
                            }

                            await context.TenantLicenses.ReplaceOneAsync(l => l.Id == license.Id, license);
                            continue;
                        }

                        // 7-Day Reminder
                        if (timeUntilExpiry <= TimeSpan.FromDays(7) && timeUntilExpiry > TimeSpan.FromDays(1))
                        {
                            bool alreadySent = await context.SubscriptionNotificationLogs
                                .Find(n => n.LicenseId == license.Id && n.NotificationType == "Subscription7DayReminder" && n.Status == "Sent")
                                .AnyAsync();

                            if (!alreadySent && !string.IsNullOrWhiteSpace(contactEmail))
                            {
                                var (success, err) = await emailService.SendSubscriptionReminderEmailAsync(contactEmail, contactName, license.ExpiryDate, "Subscription7DayReminder", renewalUrl);
                                await LogNotificationAsync(context, tenant.Id, license.Id, "Subscription7DayReminder", contactEmail, nowUtc, success, err);
                                _logger.LogInformation("[LIFECYCLE] 7-Day subscription reminder sent to {Email} for Tenant {TenantCode}", contactEmail, tenant.TenantCode);
                            }
                        }

                        // 1-Day Reminder
                        if (timeUntilExpiry <= TimeSpan.FromDays(1) && timeUntilExpiry > TimeSpan.Zero)
                        {
                            bool alreadySent = await context.SubscriptionNotificationLogs
                                .Find(n => n.LicenseId == license.Id && n.NotificationType == "Subscription1DayReminder" && n.Status == "Sent")
                                .AnyAsync();

                            if (!alreadySent && !string.IsNullOrWhiteSpace(contactEmail))
                            {
                                var (success, err) = await emailService.SendSubscriptionReminderEmailAsync(contactEmail, contactName, license.ExpiryDate, "Subscription1DayReminder", renewalUrl);
                                await LogNotificationAsync(context, tenant.Id, license.Id, "Subscription1DayReminder", contactEmail, nowUtc, success, err);
                                _logger.LogInformation("[LIFECYCLE] 1-Day subscription reminder sent to {Email} for Tenant {TenantCode}", contactEmail, tenant.TenantCode);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[LIFECYCLE] Error evaluating license {LicenseId}: {Message}", license.Id, ex.Message);
                }
            }
        }

        private static async Task LogNotificationAsync(
            MongoDbContext context,
            string tenantId,
            string licenseId,
            string notificationType,
            string recipientEmail,
            DateTime triggerTime,
            bool success,
            string? error)
        {
            var log = new SubscriptionNotificationLog
            {
                TenantId = tenantId,
                LicenseId = licenseId,
                NotificationType = notificationType,
                RecipientEmail = recipientEmail,
                TriggerAtUtc = triggerTime,
                SentAtUtc = success ? DateTime.UtcNow : null,
                Status = success ? "Sent" : "Failed",
                ErrorMessage = error,
                CreatedAtUtc = DateTime.UtcNow
            };

            await context.SubscriptionNotificationLogs.InsertOneAsync(log);
        }
    }
}
