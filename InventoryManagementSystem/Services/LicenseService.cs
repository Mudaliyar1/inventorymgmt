using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using InventoryManagementSystem.Extensions;

namespace InventoryManagementSystem.Services
{
    public class LicenseService : ILicenseService
    {
        private readonly MongoDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public LicenseService(MongoDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
        }

        public async Task<TenantLicense?> GetActiveLicenseAsync(string tenantId)
        {
            if (string.IsNullOrEmpty(tenantId)) return null;
            var license = await _context.TenantLicenses
                .Find(l => l.TenantId == tenantId)
                .SortByDescending(l => l.ExpiryDate)
                .FirstOrDefaultAsync();

            if (license != null && license.ExpiryDate <= DateTime.UtcNow)
            {
                var previousStatus = license.Status;
                if (license.Status != "Expired")
                {
                    license.Status = "Expired";
                    license.UpdatedAt = DateTime.UtcNow;
                    await _context.TenantLicenses.ReplaceOneAsync(l => l.Id == license.Id, license);

                    try
                    {
                        await _context.Tenants.UpdateOneAsync(
                            t => t.Id == tenantId,
                            Builders<Tenant>.Update.Set(t => t.Status, "Expired").Set(t => t.UpdatedAt, DateTime.UtcNow));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LICENSE NOTICE] Tenant status update error: {ex.Message}");
                    }
                }

                if (!license.ExpirationEmailSent)
                {
                    _ = TriggerExpiryEmailNotificationAsync(license, previousStatus);
                }
            }

            return license;
        }

        private async Task TriggerExpiryEmailNotificationAsync(TenantLicense license, string previousStatus)
        {
            try
            {
                if (license == null || license.ExpirationEmailSent) return;

                bool alreadySent = await _context.SubscriptionNotificationLogs
                    .Find(n => n.LicenseId == license.Id && (n.NotificationType == "TrialExpired" || n.NotificationType == "SubscriptionExpired") && n.Status == "Sent")
                    .AnyAsync();

                if (alreadySent)
                {
                    license.ExpirationEmailSent = true;
                    await _context.TenantLicenses.UpdateOneAsync(l => l.Id == license.Id, Builders<TenantLicense>.Update.Set(l => l.ExpirationEmailSent, true));
                    return;
                }

                var tenant = await _context.Tenants.Find(t => t.Id == license.TenantId).FirstOrDefaultAsync();
                if (tenant == null) return;

                string contactEmail = tenant.ContactEmail;
                string contactName = !string.IsNullOrWhiteSpace(tenant.OwnerName) ? tenant.OwnerName : tenant.ShopName;

                if (string.IsNullOrWhiteSpace(contactEmail))
                {
                    var adminUser = await _context.Users
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

                if (string.IsNullOrWhiteSpace(contactEmail)) return;

                using var scope = _serviceProvider.CreateScope();
                var emailService = scope.ServiceProvider.GetService<IEmailService>();
                if (emailService == null) return;

                var settingsService = scope.ServiceProvider.GetService<IPlatformSettingsService>();
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

                var pkg = !string.IsNullOrEmpty(license.PackageId)
                    ? await _context.SubscriptionPackages.Find(p => p.Id == license.PackageId).FirstOrDefaultAsync()
                    : null;

                bool isTrial = previousStatus == "Trial" || (pkg != null && pkg.MonthlyPrice == 0 && string.IsNullOrEmpty(license.LastPaymentId));
                string notifType = isTrial ? "TrialExpired" : "SubscriptionExpired";

                bool success = false;
                string? errorMsg = null;

                if (isTrial)
                {
                    (success, errorMsg) = await emailService.SendTrialExpiredEmailAsync(contactEmail, contactName, license.ExpiryDate, pricingUrl);
                }
                else
                {
                    (success, errorMsg) = await emailService.SendSubscriptionExpiredEmailAsync(contactEmail, contactName, license.ExpiryDate, pricingUrl);
                }

                var notifLog = new SubscriptionNotificationLog
                {
                    TenantId = tenant.Id,
                    LicenseId = license.Id,
                    NotificationType = notifType,
                    RecipientEmail = contactEmail,
                    TriggerAtUtc = DateTime.UtcNow,
                    SentAtUtc = success ? DateTime.UtcNow : null,
                    Status = success ? "Sent" : "Failed",
                    ErrorMessage = errorMsg
                };
                await _context.SubscriptionNotificationLogs.InsertOneAsync(notifLog);

                if (success)
                {
                    license.ExpirationEmailSent = true;
                    await _context.TenantLicenses.UpdateOneAsync(l => l.Id == license.Id, Builders<TenantLicense>.Update.Set(l => l.ExpirationEmailSent, true));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LICENSE SERVICE] Error triggering expiry email: {ex.Message}");
            }
        }

        public async Task<bool> IsLicenseValidAsync(string tenantId)
        {
            if (string.IsNullOrEmpty(tenantId)) return true; // SuperAdmin or unassigned system default
            var license = await GetActiveLicenseAsync(tenantId);
            if (license == null) return false;

            if (license.Status == "Suspended" || license.Status == "Cancelled") return false;
            return license.ExpiryDate >= DateTime.UtcNow;
        }

        public async Task<SubscriptionPackage?> GetTenantPackageAsync(string tenantId)
        {
            if (string.IsNullOrEmpty(tenantId)) return null;

            var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
            if (tenant != null && !string.IsNullOrEmpty(tenant.PackageId))
            {
                var package = await _context.SubscriptionPackages.Find(p => p.Id == tenant.PackageId).FirstOrDefaultAsync();
                if (package != null) return package;
            }

            var license = await GetActiveLicenseAsync(tenantId);
            if (license != null && !string.IsNullOrEmpty(license.PackageId))
            {
                return await _context.SubscriptionPackages.Find(p => p.Id == license.PackageId).FirstOrDefaultAsync();
            }

            // Fallback default
            return await _context.SubscriptionPackages.Find(p => p.IsActive).SortBy(p => p.DisplayOrder).FirstOrDefaultAsync();
        }

        public async Task<(bool Allowed, string Message)> CheckPackageLimitAsync(string tenantId, string resourceType)
        {
            if (string.IsNullOrEmpty(tenantId)) return (true, string.Empty);

            var package = await GetTenantPackageAsync(tenantId);
            if (package == null) return (true, string.Empty);

            if (string.Equals(resourceType, "Products", StringComparison.OrdinalIgnoreCase))
            {
                if (package.MaxProducts > 0)
                {
                    var count = await _context.Products.CountDocumentsAsync(p => p.TenantId == tenantId);
                    if (count >= package.MaxProducts)
                    {
                        return (false, $"Package Limit Reached: Your current package ('{package.Name}') allows a maximum of {package.MaxProducts} products. Please upgrade your subscription plan to add more products.");
                    }
                }
            }
            else if (string.Equals(resourceType, "Employees", StringComparison.OrdinalIgnoreCase))
            {
                if (package.MaxEmployees > 0)
                {
                    var count = await _context.Users.CountDocumentsAsync(u => u.TenantId == tenantId && u.Role != Role.Admin && u.Role != Role.SuperAdmin);
                    if (count >= package.MaxEmployees)
                    {
                        return (false, $"Package Limit Reached: Your current package ('{package.Name}') allows a maximum of {package.MaxEmployees} staff employees. Please upgrade your subscription plan to add more employees.");
                    }
                }
            }
            else if (string.Equals(resourceType, "Admins", StringComparison.OrdinalIgnoreCase) || string.Equals(resourceType, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                if (package.MaxAdmins > 0)
                {
                    var count = await _context.Users.CountDocumentsAsync(u => u.TenantId == tenantId && u.Role == Role.Admin);
                    if (count >= package.MaxAdmins)
                    {
                        return (false, $"Package Limit Reached: Your current package ('{package.Name}') allows a maximum of {package.MaxAdmins} Shop Administrators. Please upgrade your subscription plan to add more shop admin accounts.");
                    }
                }
            }
            else if (string.Equals(resourceType, "Suppliers", StringComparison.OrdinalIgnoreCase))
            {
                if (package.MaxSuppliers > 0)
                {
                    var count = await _context.Suppliers.CountDocumentsAsync(s => s.TenantId == tenantId);
                    if (count >= package.MaxSuppliers)
                    {
                        return (false, $"Package Limit Reached: Your current package ('{package.Name}') allows a maximum of {package.MaxSuppliers} suppliers. Please upgrade your subscription plan to add more suppliers.");
                    }
                }
            }

            return (true, string.Empty);
        }

        public async Task<bool> CanAccessModuleAsync(string tenantId, string moduleName)
        {
            if (string.IsNullOrEmpty(tenantId)) return true; // SuperAdmin has access to all

            var isValid = await IsLicenseValidAsync(tenantId);
            if (!isValid) return false;

            var package = await GetTenantPackageAsync(tenantId);
            if (package == null || package.EnabledModules == null) return true;

            // Check if module is enabled in package
            return package.EnabledModules.Any(m => m.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<TenantLicense> CreateTrialLicenseAsync(string tenantId, string packageId, int days = 14)
        {
            return await CreateTrialLicenseAsync(tenantId, packageId, TimeSpan.FromDays(days));
        }

        public async Task<TenantLicense> CreateTrialLicenseAsync(string tenantId, string packageId, TimeSpan trialDuration)
        {
            var now = DateTime.UtcNow;
            var license = new TenantLicense
            {
                TenantId = tenantId,
                PackageId = packageId,
                StartDate = now,
                ExpiryDate = now.Add(trialDuration),
                Status = "Trial",
                AutoRenew = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.TenantLicenses.InsertOneAsync(license);

            try
            {
                await _context.Tenants.UpdateOneAsync(
                    t => t.Id == tenantId,
                    Builders<Tenant>.Update.Set(t => t.Status, "Trial").Set(t => t.PackageId, packageId).Set(t => t.UpdatedAt, now));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LICENSE NOTICE] Tenant status update error: {ex.Message}");
            }

            return license;
        }

        public async Task<TenantLicense> CreateTrialLicenseFromPackageAsync(string tenantId, string packageId)
        {
            var duration = TimeSpan.FromDays(14);
            if (!string.IsNullOrEmpty(packageId))
            {
                var pkg = await _context.SubscriptionPackages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
                if (pkg != null)
                {
                    duration = pkg.GetTrialTimeSpan();
                    if (duration <= TimeSpan.Zero)
                    {
                        duration = TimeSpan.FromDays(14);
                    }
                }
            }
            return await CreateTrialLicenseAsync(tenantId, packageId, duration);
        }

        public async Task<bool> RenewOrExtendLicenseAsync(string tenantId, string packageId, int durationDays)
        {
            var existing = await GetActiveLicenseAsync(tenantId);
            var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();

            bool isUpgradeFromTrial = existing == null || existing.Status == "Trial" || tenant?.IsPaidCustomer != true;
            bool isPackageChange = existing != null && existing.PackageId != packageId;

            if (existing != null)
            {
                DateTime newStart;
                if (isUpgradeFromTrial || isPackageChange || existing.ExpiryDate <= DateTime.UtcNow)
                {
                    // Paid plan purchase or package change starts fresh from current time
                    newStart = DateTime.UtcNow;
                    existing.StartDate = DateTime.UtcNow;
                }
                else
                {
                    // Same paid package renewal extends existing active paid validity
                    newStart = existing.ExpiryDate;
                }

                existing.PackageId = packageId;
                existing.ExpiryDate = newStart.AddDays(durationDays);
                existing.Status = "Active";
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.TenantLicenses.ReplaceOneAsync(l => l.Id == existing.Id, existing);

                // Also update Tenant PackageId
                if (tenant != null)
                {
                    tenant.PackageId = packageId;
                    tenant.Status = "Active";
                    tenant.IsPaidCustomer = true;
                    tenant.UpdatedAt = DateTime.UtcNow;
                    await _context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);
                }

                // If package is custom, link it to this tenant
                var pkg = await _context.SubscriptionPackages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
                if (pkg != null && (pkg.IsCustom || !string.IsNullOrEmpty(pkg.AssignedTenantId) || pkg.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase)))
                {
                    pkg.IsCustom = true;
                    pkg.AssignedTenantId = tenantId;
                    await _context.SubscriptionPackages.ReplaceOneAsync(p => p.Id == pkg.Id, pkg);
                }

                return true;
            }
            else
            {
                var license = new TenantLicense
                {
                    TenantId = tenantId,
                    PackageId = packageId,
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddDays(durationDays),
                    Status = "Active",
                    AutoRenew = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.TenantLicenses.InsertOneAsync(license);

                // If package is custom, link it to this tenant
                var pkg = await _context.SubscriptionPackages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
                if (pkg != null && (pkg.IsCustom || !string.IsNullOrEmpty(pkg.AssignedTenantId) || pkg.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase)))
                {
                    pkg.IsCustom = true;
                    pkg.AssignedTenantId = tenantId;
                    await _context.SubscriptionPackages.ReplaceOneAsync(p => p.Id == pkg.Id, pkg);
                }

                return true;
            }
        }

        public async Task<(bool Success, string Message, DateTime NewExpiry)> UpdateCustomPackageDurationAsync(
            string tenantId, string packageId, int days, int hours, int minutes, int seconds, bool isFromNow = true)
        {
            var pkg = await _context.SubscriptionPackages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
            if (pkg == null)
            {
                return (false, "Subscription package not found.", DateTime.UtcNow);
            }

            bool isCustom = pkg.IsCustom || !string.IsNullOrEmpty(pkg.AssignedTenantId) || pkg.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase);
            if (!isCustom)
            {
                return (false, "Fine-grained custom duration down to seconds is only permitted for Custom Package shops.", DateTime.UtcNow);
            }

            var durationSpan = new TimeSpan(days, hours, minutes, seconds);
            if (durationSpan.TotalSeconds < 1)
            {
                return (false, "Duration must be at least 1 second.", DateTime.UtcNow);
            }

            var existing = await GetActiveLicenseAsync(tenantId);
            DateTime newExpiry;
            if (existing != null)
            {
                if (isFromNow)
                {
                    newExpiry = DateTime.UtcNow.Add(durationSpan);
                }
                else
                {
                    var start = existing.ExpiryDate > DateTime.UtcNow ? existing.ExpiryDate : DateTime.UtcNow;
                    newExpiry = start.Add(durationSpan);
                }

                existing.PackageId = packageId;
                existing.ExpiryDate = newExpiry;
                existing.Status = newExpiry > DateTime.UtcNow ? "Active" : "Expired";
                if (newExpiry > DateTime.UtcNow)
                {
                    existing.ExpirationEmailSent = false;
                    existing.SuspensionEmailSent = false;
                }
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.TenantLicenses.ReplaceOneAsync(l => l.Id == existing.Id, existing);
            }
            else
            {
                newExpiry = DateTime.UtcNow.Add(durationSpan);
                var license = new TenantLicense
                {
                    TenantId = tenantId,
                    PackageId = packageId,
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = newExpiry,
                    Status = newExpiry > DateTime.UtcNow ? "Active" : "Expired",
                    AutoRenew = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.TenantLicenses.InsertOneAsync(license);
            }

            var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
            if (tenant != null)
            {
                tenant.PackageId = packageId;
                tenant.Status = newExpiry > DateTime.UtcNow ? "Active" : "Expired";
                tenant.UpdatedAt = DateTime.UtcNow;
                await _context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);
            }

            pkg.IsCustom = true;
            pkg.AssignedTenantId = tenantId;
            await _context.SubscriptionPackages.ReplaceOneAsync(p => p.Id == pkg.Id, pkg);

            return (true, $"Custom duration applied successfully. Expiry set to {newExpiry.ToIstString("dd-MMM-yyyy hh:mm:ss tt IST")}.", newExpiry);
        }

        public async Task<IEnumerable<SubscriptionPackage>> GetAllPackagesAsync()
        {
            return await _context.SubscriptionPackages.Find(_ => true).SortBy(p => p.DisplayOrder).ToListAsync();
        }

        public async Task<IEnumerable<SubscriptionPackage>> GetStandardPackagesAsync()
        {
            // Returns standard SaaS packages for Super Admin directory (excludes custom & shop-specific packages)
            var all = await _context.SubscriptionPackages.Find(_ => true).SortBy(p => p.DisplayOrder).ToListAsync();
            return all.Where(p => !p.IsCustom && string.IsNullOrEmpty(p.AssignedTenantId) && !p.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<IEnumerable<SubscriptionPackage>> GetPublicPackagesAsync()
        {
            // Returns active public packages for landing & pricing pages
            var all = await _context.SubscriptionPackages.Find(p => p.IsActive).SortBy(p => p.DisplayOrder).ToListAsync();
            return all.Where(p => !p.IsCustom && string.IsNullOrEmpty(p.AssignedTenantId) && !p.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<IEnumerable<SubscriptionPackage>> GetCustomPackagesAsync()
        {
            // Returns custom packages tailored for specific shops or custom VIP packages
            var all = await _context.SubscriptionPackages.Find(_ => true).SortByDescending(p => p.CreatedAt).ToListAsync();
            return all.Where(p => p.IsCustom || !string.IsNullOrEmpty(p.AssignedTenantId) || p.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<SubscriptionPackage?> GetPackageByIdAsync(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return null;
            return await _context.SubscriptionPackages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
        }

        public async Task CreateOrUpdatePackageAsync(SubscriptionPackage package)
        {
            // Auto-detect custom packages: if AssignedTenantId is present or name contains "Custom", mark IsCustom = true
            if (!string.IsNullOrEmpty(package.AssignedTenantId) || package.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase))
            {
                package.IsCustom = true;
            }

            if (string.IsNullOrEmpty(package.Id))
            {
                package.CreatedAt = DateTime.UtcNow;
                await _context.SubscriptionPackages.InsertOneAsync(package);
            }
            else
            {
                var existing = await _context.SubscriptionPackages.Find(p => p.Id == package.Id).FirstOrDefaultAsync();
                if (existing != null)
                {
                    // Preserve custom package state so it NEVER reverts to a public/normal package
                    if (existing.IsCustom || !string.IsNullOrEmpty(existing.AssignedTenantId) || existing.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase))
                    {
                        package.IsCustom = true;
                        if (string.IsNullOrEmpty(package.AssignedTenantId))
                        {
                            package.AssignedTenantId = existing.AssignedTenantId;
                        }
                    }
                    if (package.CreatedAt == default)
                    {
                        package.CreatedAt = existing.CreatedAt;
                    }
                }
                await _context.SubscriptionPackages.ReplaceOneAsync(p => p.Id == package.Id, package);
            }
        }
    }
}
