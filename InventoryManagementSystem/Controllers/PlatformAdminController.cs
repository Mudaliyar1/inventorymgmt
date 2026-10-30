using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using InventoryManagementSystem.Services;
using InventoryManagementSystem.Extensions;

namespace InventoryManagementSystem.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Route("platform/[action]")]
    public class PlatformAdminController : Controller
    {
        private readonly MongoDbContext _context;
        private readonly ILicenseService _licenseService;
        private readonly IAuthService _authService;
        private readonly IFeatureCatalogService _featureCatalogService;
        private readonly IPlatformSettingsService _platformSettingsService;
        private readonly IImageService _imageService;
        private readonly IAuditLogService _auditLogService;
        private readonly IEmailService _emailService;
        private readonly ICustomPricingService _customPricingService;
        private readonly IContactInquiryService _contactInquiryService;
        private readonly IPolicyService _policyService;
        private readonly ISaaSPaymentService _saasPaymentService;
        private readonly IReceiptService _receiptService;
        private readonly IRazorpayPaymentService _razorpayPaymentService;

        public PlatformAdminController(
            MongoDbContext context,
            ILicenseService licenseService,
            IAuthService authService,
            IFeatureCatalogService featureCatalogService,
            IPlatformSettingsService platformSettingsService,
            IImageService imageService,
            IAuditLogService auditLogService,
            IEmailService emailService,
            ICustomPricingService customPricingService,
            IContactInquiryService contactInquiryService,
            IPolicyService policyService,
            ISaaSPaymentService saasPaymentService,
            IReceiptService receiptService,
            IRazorpayPaymentService razorpayPaymentService)
        {
            _context = context;
            _licenseService = licenseService;
            _authService = authService;
            _featureCatalogService = featureCatalogService;
            _platformSettingsService = platformSettingsService;
            _imageService = imageService;
            _auditLogService = auditLogService;
            _emailService = emailService;
            _customPricingService = customPricingService;
            _contactInquiryService = contactInquiryService;
            _policyService = policyService;
            _saasPaymentService = saasPaymentService;
            _receiptService = receiptService;
            _razorpayPaymentService = razorpayPaymentService;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var tenants = await _context.Tenants.Find(_ => true).ToListAsync();
            var licenses = await _context.TenantLicenses.Find(_ => true).ToListAsync();
            var packages = await _context.SubscriptionPackages.Find(_ => true).ToListAsync();
            var payments = await _context.SaaSPaymentTransactions.Find(_ => true).ToListAsync();
            var renewalRequests = await _context.LicenseRenewalRequests.Find(_ => true).SortByDescending(r => r.RequestedAt).ToListAsync();

            ViewBag.TotalTenants = tenants.Count;
            ViewBag.ActiveTenants = tenants.Count(t => t.Status == "Active");
            ViewBag.TrialTenants = tenants.Count(t => t.Status == "Trial" || licenses.Any(l => l.TenantId == t.Id && l.Status == "Trial"));
            ViewBag.ExpiredTenants = licenses.Count(l => l.ExpiryDate < DateTime.UtcNow);
            ViewBag.PendingRenewalCount = renewalRequests.Count(r => r.Status == "Pending");
            ViewBag.RenewalRequests = renewalRequests;

            var totalRevenue = payments.Where(p => p.Status == "Success").Sum(p => p.Amount);
            ViewBag.TotalRevenue = totalRevenue;

            // Estimated MRR based on active tenant packages
            decimal mrr = 0;
            foreach (var t in tenants.Where(t => t.Status == "Active"))
            {
                var pkg = packages.FirstOrDefault(p => p.Id == t.PackageId);
                if (pkg != null) mrr += pkg.MonthlyPrice;
            }
            ViewBag.MRR = mrr;

            ViewBag.RecentTenants = tenants.OrderByDescending(t => t.CreatedAt).Take(5).ToList();
            ViewBag.RecentPayments = payments.OrderByDescending(p => p.PaymentDate).Take(5).ToList();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Tenants(string? search, string? statusFilter)
        {
            var filterBuilder = Builders<Tenant>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrWhiteSpace(search))
            {
                filter &= filterBuilder.Or(
                    filterBuilder.Regex(t => t.ShopName, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(t => t.OwnerName, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(t => t.ContactEmail, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(t => t.TenantCode, new MongoDB.Bson.BsonRegularExpression(search, "i"))
                );
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                filter &= filterBuilder.Eq(t => t.Status, statusFilter);
            }

            var tenants = await _context.Tenants.Find(filter).SortByDescending(t => t.CreatedAt).ToListAsync();
            var licenses = await _context.TenantLicenses.Find(_ => true).ToListAsync();
            var packages = await _context.SubscriptionPackages.Find(_ => true).ToListAsync();

            var allSuppliers = await _context.Suppliers.Find(_ => true).ToListAsync();
            var allUsers = await _context.Users.Find(_ => true).ToListAsync();
            var allProducts = await _context.Products.Find(_ => true).ToListAsync();
            var allCustomers = await _context.Customers.Find(_ => true).ToListAsync();
            var allSales = await _context.Sales.Find(_ => true).ToListAsync();

            ViewBag.Licenses = licenses.ToDictionary(l => l.TenantId, l => l);
            ViewBag.Packages = packages.ToDictionary(p => p.Id, p => p);
            ViewBag.SupplierCounts = allSuppliers.Where(s => !string.IsNullOrEmpty(s.TenantId)).GroupBy(s => s.TenantId!).ToDictionary(g => g.Key, g => (long)g.Count());
            ViewBag.EmployeeCounts = allUsers.Where(u => !string.IsNullOrEmpty(u.TenantId) && u.Role != Role.Admin && u.Role != Role.SuperAdmin).GroupBy(u => u.TenantId!).ToDictionary(g => g.Key, g => (long)g.Count());
            ViewBag.ProductCounts = allProducts.Where(p => !string.IsNullOrEmpty(p.TenantId)).GroupBy(p => p.TenantId!).ToDictionary(g => g.Key, g => (long)g.Count());
            ViewBag.CustomerCounts = allCustomers.Where(c => !string.IsNullOrEmpty(c.TenantId)).GroupBy(c => c.TenantId!).ToDictionary(g => g.Key, g => (long)g.Count());
            ViewBag.SalesCounts = allSales.Where(s => !string.IsNullOrEmpty(s.TenantId)).GroupBy(s => s.TenantId!).ToDictionary(g => g.Key, g => (long)g.Count());

            return View(tenants);
        }

        [HttpGet]
        [Route("platform/TenantDetails/{id}")]
        public async Task<IActionResult> TenantDetails(string id)
        {
            var tenant = await _context.Tenants.Find(t => t.Id == id).FirstOrDefaultAsync();
            if (tenant == null)
            {
                TempData["ToastMessage"] = "Shop not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Tenants));
            }

            var license = await _context.TenantLicenses.Find(l => l.TenantId == id).FirstOrDefaultAsync();
            var package = !string.IsNullOrEmpty(tenant.PackageId)
                ? await _context.SubscriptionPackages.Find(p => p.Id == tenant.PackageId).FirstOrDefaultAsync()
                : null;
            var allPackages = await _context.SubscriptionPackages.Find(p => !p.IsCustom).ToListAsync();
            var customPackages = await _context.SubscriptionPackages.Find(p => p.IsCustom).ToListAsync();

            var suppliers = await _context.Suppliers.Find(s => s.TenantId == id).ToListAsync();
            var users = await _context.Users.Find(u => u.TenantId == id && u.Role != Role.SuperAdmin).ToListAsync();
            var products = await _context.Products.Find(p => p.TenantId == id).ToListAsync();
            var customers = await _context.Customers.Find(c => c.TenantId == id).ToListAsync();
            var sales = await _context.Sales.Find(s => s.TenantId == id).ToListAsync();
            var renewalRequests = await _context.LicenseRenewalRequests.Find(r => r.TenantId == id).SortByDescending(r => r.RequestedAt).ToListAsync();

            var notificationLogs = await _context.SubscriptionNotificationLogs.Find(n => n.TenantId == id).SortByDescending(n => n.CreatedAtUtc).ToListAsync();

            ViewBag.License = license;
            ViewBag.Package = package;
            ViewBag.Packages = allPackages;
            ViewBag.CustomPackages = customPackages;
            ViewBag.Suppliers = suppliers;
            ViewBag.Users = users;
            ViewBag.RenewalRequests = renewalRequests;
            ViewBag.NotificationLogs = notificationLogs;
            ViewBag.StaffCount = users.Count(u => u.Role != Role.Admin);
            ViewBag.ProductsCount = products.Count;
            ViewBag.CustomersCount = customers.Count;
            ViewBag.SalesCount = sales.Count;

            return View(tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetrySubscriptionNotification(string logId)
        {
            var log = await _context.SubscriptionNotificationLogs.Find(l => l.Id == logId).FirstOrDefaultAsync();
            if (log == null)
            {
                TempData["ToastMessage"] = "Notification record not found.";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(Tenants));
            }

            var tenant = await _context.Tenants.Find(t => t.Id == log.TenantId).FirstOrDefaultAsync();
            var license = await _context.TenantLicenses.Find(l => l.Id == log.LicenseId).FirstOrDefaultAsync();
            if (tenant == null || license == null)
            {
                TempData["ToastMessage"] = "Shop or license associated with this notification no longer exists.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(TenantDetails), new { id = log.TenantId });
            }

            string contactEmail = !string.IsNullOrWhiteSpace(log.RecipientEmail) ? log.RecipientEmail : tenant.ContactEmail;
            string contactName = !string.IsNullOrWhiteSpace(tenant.OwnerName) ? tenant.OwnerName : tenant.ShopName;
            string baseUrl = "https://sims-saas.com";
            var settings = await _platformSettingsService.GetSettingsAsync();
            if (settings != null && !string.IsNullOrWhiteSpace(settings.SystemUrl))
            {
                baseUrl = settings.SystemUrl.TrimEnd('/');
            }
            string pricingUrl = $"{baseUrl}/Landing/Pricing";
            string renewalUrl = $"{baseUrl}/Subscription/Renew";

            bool success = false;
            string? err = null;

            switch (log.NotificationType)
            {
                case "Trial24HourReminder":
                case "Trial1HourReminder":
                    (success, err) = await _emailService.SendTrialReminderEmailAsync(contactEmail, contactName, license.ExpiryDate, log.NotificationType, pricingUrl);
                    break;
                case "TrialExpired":
                    (success, err) = await _emailService.SendTrialExpiredEmailAsync(contactEmail, contactName, license.ExpiryDate, pricingUrl);
                    break;
                case "Subscription7DayReminder":
                case "Subscription1DayReminder":
                    (success, err) = await _emailService.SendSubscriptionReminderEmailAsync(contactEmail, contactName, license.ExpiryDate, log.NotificationType, renewalUrl);
                    break;
                case "SubscriptionExpired":
                    (success, err) = await _emailService.SendSubscriptionExpiredEmailAsync(contactEmail, contactName, license.ExpiryDate, pricingUrl);
                    break;
                default:
                    (success, err) = await _emailService.SendEmailWithResultAsync(contactEmail, $"SIMS Notification — {log.NotificationType}", $"<p>Notification retry for {log.NotificationType}</p>");
                    break;
            }

            log.Status = success ? "Sent" : "Failed";
            log.SentAtUtc = success ? DateTime.UtcNow : null;
            log.ErrorMessage = err;
            await _context.SubscriptionNotificationLogs.ReplaceOneAsync(l => l.Id == log.Id, log);

            TempData["ToastMessage"] = success
                ? $"Notification '{log.NotificationType}' was resent successfully to {contactEmail}."
                : $"Notification retry failed: {err}";
            TempData["ToastType"] = success ? "success" : "danger";

            return RedirectToAction(nameof(TenantDetails), new { id = log.TenantId });
        }

        [HttpGet]
        public async Task<IActionResult> CreateTenant()
        {
            ViewBag.Packages = await _licenseService.GetStandardPackagesAsync();
            ViewBag.CustomPackages = await _licenseService.GetCustomPackagesAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTenant(
            string shopName, string ownerName, string contactEmail, string phone,
            string address, string gstin, string packageId, string adminUsername, string temporaryPassword, int durationDays = 30)
        {
            if (string.IsNullOrWhiteSpace(shopName) || string.IsNullOrWhiteSpace(contactEmail) || string.IsNullOrWhiteSpace(temporaryPassword))
            {
                TempData["ToastMessage"] = "Please fill in all required shop and admin credentials.";
                TempData["ToastType"] = "danger";
                ViewBag.Packages = await _licenseService.GetStandardPackagesAsync();
                ViewBag.CustomPackages = await _licenseService.GetCustomPackagesAsync();
                return View();
            }

            // Generate unique TenantCode
            var tenantCount = await _context.Tenants.CountDocumentsAsync(_ => true);
            var tenantCode = $"SHOP-{100 + tenantCount + 1}";

            var tenant = new Tenant
            {
                TenantCode = tenantCode,
                ShopName = shopName,
                OwnerName = ownerName,
                ContactEmail = contactEmail,
                Phone = phone,
                Address = address,
                GSTIN = gstin,
                PackageId = packageId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Tenants.InsertOneAsync(tenant);

            // Create Clean Shop Admin User with newly created tenant.Id
            var shopAdmin = new User
            {
                TenantId = tenant.Id,
                Username = string.IsNullOrWhiteSpace(adminUsername) ? contactEmail : adminUsername,
                Email = contactEmail,
                FullName = ownerName,
                PhoneNumber = phone,
                Role = Role.Admin,
                EmployeeId = "EMP-0001",
                IsLocked = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            await _context.Users.InsertOneAsync(shopAdmin);

            // Create License for Shop
            await _licenseService.RenewOrExtendLicenseAsync(tenant.Id, packageId, durationDays);

            TempData["ToastMessage"] = $"New shop '{shopName}' ({tenantCode}) created successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Tenants));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTenantStatus(string tenantId, string status, string? returnUrl = null)
        {
            var tenant = await _context.Tenants.Find(t => t.Id == tenantId || t.TenantCode == tenantId).FirstOrDefaultAsync();
            if (tenant != null)
            {
                tenant.Status = status;
                tenant.UpdatedAt = DateTime.UtcNow;
                await _context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);

                // Synchronize TenantLicense status
                var license = await _context.TenantLicenses
                    .Find(l => l.TenantId == tenant.Id || l.TenantId == tenant.TenantCode)
                    .SortByDescending(l => l.ExpiryDate)
                    .FirstOrDefaultAsync();

                if (license != null)
                {
                    if (string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase))
                    {
                        license.Status = "Suspended";
                    }
                    else if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        license.Status = license.ExpiryDate < DateTime.UtcNow ? "Expired" : "Active";
                    }
                    license.UpdatedAt = DateTime.UtcNow;
                    await _context.TenantLicenses.ReplaceOneAsync(l => l.Id == license.Id, license);
                }

                // Send notification email to the shop owner
                try
                {
                    if (!string.IsNullOrWhiteSpace(tenant.ContactEmail))
                    {
                        if (string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase))
                        {
                            await _emailService.SendShopSuspendedEmailAsync(tenant.ContactEmail, tenant.ShopName, tenant.OwnerName);
                        }
                        else if (string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase))
                        {
                            await _emailService.SendLicenseExpiredEmailAsync(tenant.ContactEmail, tenant.ShopName, tenant.OwnerName, license?.ExpiryDate ?? DateTime.UtcNow);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL ERROR] Failed to send {status} email to {tenant.ContactEmail}: {ex.Message}");
                }

                TempData["ToastMessage"] = $"Shop '{tenant.ShopName}' status updated to {status}.";
                TempData["ToastType"] = string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase) ? "warning" : "success";
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(TenantDetails), new { id = tenantId });
        }

        private static bool VerifyAdminPassword(string? enteredPassword, string? storedHash)
        {
            if (string.IsNullOrWhiteSpace(enteredPassword) || string.IsNullOrWhiteSpace(storedHash))
                return false;

            var raw = enteredPassword;
            var trimmed = enteredPassword.Trim();

            try
            {
                if (storedHash.StartsWith("$2"))
                {
                    if (BCrypt.Net.BCrypt.Verify(raw, storedHash)) return true;
                    if (BCrypt.Net.BCrypt.Verify(trimmed, storedHash)) return true;
                }
            }
            catch
            {
                // Ignore BCrypt salt format exceptions
            }

            // Direct comparison fallback for plain/legacy hashes
            if (string.Equals(storedHash, raw, StringComparison.Ordinal) ||
                string.Equals(storedHash, trimmed, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTenant(string tenantId, string adminPassword, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                TempData["ToastMessage"] = "Security Check Failed: Super Admin password is required to delete a shop.";
                TempData["ToastType"] = "danger";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(Tenants));
            }

            // Verify Super Admin Password against currently authenticated user ID, username/email, or any active SuperAdmin
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var currentUsername = User.Identity?.Name;
            var currentEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            bool isPasswordValid = false;

            // 1. Check currently authenticated user by NameIdentifier Claim (ObjectId)
            if (!string.IsNullOrEmpty(currentUserId))
            {
                var loggedInUser = await _context.Users.Find(u => u.Id == currentUserId).FirstOrDefaultAsync();
                if (loggedInUser != null && VerifyAdminPassword(adminPassword, loggedInUser.PasswordHash))
                {
                    isPasswordValid = true;
                }
            }

            // 2. Check by current username or email if not already matched
            if (!isPasswordValid && !string.IsNullOrEmpty(currentUsername))
            {
                var userByName = await _context.Users.Find(u => 
                    (u.Role == Role.SuperAdmin || u.Role == Role.Admin) && 
                    (u.Username == currentUsername || u.Email == currentUsername)
                ).FirstOrDefaultAsync();

                if (userByName != null && VerifyAdminPassword(adminPassword, userByName.PasswordHash))
                {
                    isPasswordValid = true;
                }
            }

            // 3. Fallback: Check against ANY registered SuperAdmin or Platform Admin in the system
            if (!isPasswordValid)
            {
                var superAdmins = await _context.Users.Find(u => 
                    u.Role == Role.SuperAdmin || (u.Role == Role.Admin && (u.TenantId == null || u.TenantId == ""))
                ).ToListAsync();

                foreach (var sa in superAdmins)
                {
                    if (VerifyAdminPassword(adminPassword, sa.PasswordHash))
                    {
                        isPasswordValid = true;
                        break;
                    }
                }
            }

            if (!isPasswordValid)
            {
                TempData["ToastMessage"] = "Security Check Failed: Incorrect Super Admin Password. Shop deletion cancelled.";
                TempData["ToastType"] = "danger";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(Tenants));
            }

            var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
            if (tenant == null)
            {
                TempData["ToastMessage"] = "Shop not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Tenants));
            }

            // Cascade delete any custom packages created specifically for or assigned to this shop safely
            if (!string.IsNullOrEmpty(tenant.PackageId))
            {
                var pkg = await _context.SubscriptionPackages.Find(p => p.Id == tenant.PackageId).FirstOrDefaultAsync();
                if (pkg != null && (pkg.IsCustom || (!string.IsNullOrEmpty(pkg.Name) && pkg.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase))))
                {
                    await _context.SubscriptionPackages.DeleteOneAsync(p => p.Id == pkg.Id);
                }
            }
            await _context.SubscriptionPackages.DeleteManyAsync(p => p.AssignedTenantId == tenantId);

            // Clean up all collections for this tenant
            await _context.Tenants.DeleteOneAsync(t => t.Id == tenantId);
            await _context.TenantLicenses.DeleteManyAsync(l => l.TenantId == tenantId);
            await _context.Users.DeleteManyAsync(u => u.TenantId == tenantId && u.Role != Role.SuperAdmin);
            await _context.Suppliers.DeleteManyAsync(s => s.TenantId == tenantId);
            await _context.Products.DeleteManyAsync(p => p.TenantId == tenantId);
            await _context.Customers.DeleteManyAsync(c => c.TenantId == tenantId);
            await _context.Sales.DeleteManyAsync(s => s.TenantId == tenantId);
            await _context.Categories.DeleteManyAsync(c => c.TenantId == tenantId);
            await _context.Devices.DeleteManyAsync(d => d.TenantId == tenantId);
            await _context.StockTransactions.DeleteManyAsync(st => st.TenantId == tenantId);
            await _context.ReturnRecords.DeleteManyAsync(rr => rr.TenantId == tenantId);
            await _context.ExchangeRecords.DeleteManyAsync(er => er.TenantId == tenantId);
            await _context.RepairTickets.DeleteManyAsync(rt => rt.TenantId == tenantId);
            await _context.SupplierOrders.DeleteManyAsync(so => so.TenantId == tenantId);
            await _context.SupplierPurchaseReturns.DeleteManyAsync(spr => spr.TenantId == tenantId);
            await _context.AuditLogs.DeleteManyAsync(al => al.TenantId == tenantId);
            await _context.Notifications.DeleteManyAsync(n => n.TenantId == tenantId);
            await _context.Settings.DeleteManyAsync(s => s.TenantId == tenantId);
            await _context.SaaSPaymentTransactions.DeleteManyAsync(spt => spt.TenantId == tenantId);
            await _context.LicenseRenewalRequests.DeleteManyAsync(r => r.TenantId == tenantId);
            await _context.SubscriptionNotificationLogs.DeleteManyAsync(snl => snl.TenantId == tenantId);

            await _auditLogService.LogActivityAsync("SHOP_DELETED", User.Identity?.Name ?? "SuperAdmin", tenant.Id, $"Deleted shop '{tenant.ShopName}' ({tenant.TenantCode}) and associated collections.");

            TempData["ToastMessage"] = $"Shop '{tenant.ShopName}' ({tenant.TenantCode}) and all associated data deleted successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Tenants));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExtendLicense(string tenantId, string packageId, int extraDays, string? returnUrl = null)
        {
            await _licenseService.RenewOrExtendLicenseAsync(tenantId, packageId, extraDays);
            TempData["ToastMessage"] = $"License extended by {extraDays} days successfully.";
            TempData["ToastType"] = "success";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(TenantDetails), new { id = tenantId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCustomDuration(
            string tenantId, string packageId, int days, int hours, int minutes, int seconds, string updateMode = "fromNow", string? returnUrl = null)
        {
            bool isFromNow = string.Equals(updateMode, "fromNow", StringComparison.OrdinalIgnoreCase);
            var (success, message, newExpiry) = await _licenseService.UpdateCustomPackageDurationAsync(
                tenantId, packageId, days, hours, minutes, seconds, isFromNow);

            if (success)
            {
                var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
                await _auditLogService.LogExAsync(
                    "LICENSE_CUSTOM_DURATION_UPDATED",
                    "Platform SaaS Admin",
                    tenant?.ShopName ?? tenantId,
                    $"Custom license duration set for shop '{tenant?.ShopName}' ({days}d {hours}h {minutes}m {seconds}s, Mode: {updateMode}). New Expiry: {newExpiry.ToIstString("dd-MMM-yyyy hh:mm:ss tt IST")}.",
                    "Success",
                    "Information");

                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "danger";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(TenantDetails), new { id = tenantId });
        }

        [HttpGet]
        public async Task<IActionResult> Packages()
        {
            var packages = await _licenseService.GetStandardPackagesAsync();
            var tenants = await _context.Tenants.Find(_ => true).ToListAsync();

            var subscriberCounts = new Dictionary<string, int>();
            foreach (var pkg in packages)
            {
                subscriberCounts[pkg.Id] = tenants.Count(t => t.PackageId == pkg.Id);
            }
            ViewBag.SubscriberCounts = subscriberCounts;

            return View(packages);
        }

        [HttpGet]
        public async Task<IActionResult> CustomPackages()
        {
            var customPackages = await _licenseService.GetCustomPackagesAsync();
            var tenants = await _context.Tenants.Find(_ => true).ToListAsync();

            var tenantDict = tenants.ToDictionary(t => t.Id, t => t);
            ViewBag.Tenants = tenantDict;
            ViewBag.AllTenantsList = tenants;

            var subscriberCounts = new Dictionary<string, int>();
            foreach (var pkg in customPackages)
            {
                subscriberCounts[pkg.Id] = tenants.Count(t => t.PackageId == pkg.Id);
            }
            ViewBag.SubscriberCounts = subscriberCounts;
            ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();

            return View(customPackages);
        }

        [HttpGet]
        public async Task<IActionResult> PackageDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ToastMessage"] = "Package ID is required.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(CustomPackages));
            }

            var package = await _context.SubscriptionPackages.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (package == null)
            {
                TempData["ToastMessage"] = "Subscription package not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(CustomPackages));
            }

            // Find all shops subscribed to this package or explicitly assigned to it
            var subscribedShops = await _context.Tenants.Find(t => t.PackageId == id || t.Id == package.AssignedTenantId).ToListAsync();
            var allTenants = await _context.Tenants.Find(_ => true).ToListAsync();

            // Fetch active/recent licenses for subscribed shops
            var tenantIds = subscribedShops.Select(s => s.Id).ToList();
            var licenses = await _context.TenantLicenses.Find(l => tenantIds.Contains(l.TenantId)).ToListAsync();
            var licenseDict = licenses
                .GroupBy(l => l.TenantId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.ExpiryDate).FirstOrDefault());

            ViewBag.SubscribedShops = subscribedShops;
            ViewBag.Licenses = licenseDict;
            ViewBag.AllTenants = allTenants;
            ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();

            Tenant? assignedTenant = null;
            if (!string.IsNullOrEmpty(package.AssignedTenantId))
            {
                assignedTenant = allTenants.FirstOrDefault(t => t.Id == package.AssignedTenantId);
            }
            ViewBag.AssignedTenant = assignedTenant;

            return View(package);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCustomPackage(string? tenantId = null)
        {
            ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();
            ViewBag.Tenants = await _context.Tenants.Find(_ => true).ToListAsync();
            ViewBag.TargetTenantId = tenantId;

            Tenant? targetTenant = null;
            if (!string.IsNullOrEmpty(tenantId))
            {
                targetTenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
            }
            ViewBag.TargetTenant = targetTenant;

            return View(new SubscriptionPackage
            {
                Name = targetTenant != null ? $"Custom Plan - {targetTenant.ShopName}" : "Custom VIP Plan",
                Description = targetTenant != null ? $"Exclusive custom subscription package tailored for {targetTenant.ShopName}" : "Exclusive shop-tailored subscription package",
                MonthlyPrice = 2999,
                YearlyPrice = 29990,
                TrialDays = 14,
                BillingCycle = "Monthly",
                MaxEmployees = 25,
                MaxProducts = 15000,
                MaxSuppliers = 250,
                IsActive = true,
                IsCustom = true,
                AssignedTenantId = tenantId,
                DisplayOrder = 99
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomPackage(SubscriptionPackage package, List<string> selectedFeatures, bool allotNow = false, int extensionDays = 30)
        {
            if (string.IsNullOrWhiteSpace(package.Name))
            {
                ModelState.AddModelError("Name", "Custom Package Name is required.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();
                ViewBag.Tenants = await _context.Tenants.Find(_ => true).ToListAsync();
                return View(package);
            }

            package.IsCustom = true;
            package.EnabledFeatures = selectedFeatures ?? new List<string>();
            package.EnabledModules = package.EnabledFeatures;
            package.CreatedAt = DateTime.UtcNow;

            await _licenseService.CreateOrUpdatePackageAsync(package);

            if (allotNow && !string.IsNullOrEmpty(package.AssignedTenantId))
            {
                await _licenseService.RenewOrExtendLicenseAsync(package.AssignedTenantId, package.Id, extensionDays);
                TempData["ToastMessage"] = $"Custom Package '{package.Name}' created and immediately allotted to shop for {extensionDays} days!";
            }
            else
            {
                TempData["ToastMessage"] = $"Custom Package '{package.Name}' created successfully!";
            }

            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(CustomPackages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AllotCustomPackage(string tenantId, string packageId, int extensionDays = 30, string? returnUrl = null)
        {
            var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
            var package = await _context.SubscriptionPackages.Find(p => p.Id == packageId).FirstOrDefaultAsync();

            if (tenant == null || package == null)
            {
                TempData["ToastMessage"] = "Invalid shop or package selection.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(CustomPackages));
            }

            if (package.IsCustom && string.IsNullOrEmpty(package.AssignedTenantId))
            {
                package.AssignedTenantId = tenantId;
                await _context.SubscriptionPackages.ReplaceOneAsync(p => p.Id == package.Id, package);
            }

            await _licenseService.RenewOrExtendLicenseAsync(tenantId, packageId, extensionDays);

            TempData["ToastMessage"] = $"Custom Package '{package.Name}' allotted to '{tenant.ShopName}' for {extensionDays} days successfully.";
            TempData["ToastType"] = "success";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(TenantDetails), new { id = tenantId });
        }

        [HttpGet]
        public async Task<IActionResult> CreatePackage()
        {
            ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();
            return View(new SubscriptionPackage
            {
                MonthlyPrice = 1999,
                YearlyPrice = 19990,
                TrialDays = 14,
                BillingCycle = "Monthly",
                MaxEmployees = 10,
                MaxProducts = 5000,
                MaxSuppliers = 100,
                IsActive = true,
                DisplayOrder = 1
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePackage(SubscriptionPackage package, List<string> selectedFeatures)
        {
            if (string.IsNullOrWhiteSpace(package.Name))
            {
                ModelState.AddModelError("Name", "Package Name is required.");
            }

            if (package.HasTrial)
            {
                if (package.TrialDuration <= 0)
                {
                    ModelState.AddModelError("TrialDuration", "Trial Duration must be a positive number when free trials are enabled.");
                }
                else if (package.TrialDuration > 100000)
                {
                    ModelState.AddModelError("TrialDuration", "Trial Duration cannot exceed 100,000.");
                }

                var unit = package.TrialDurationUnit?.Trim();
                if (string.IsNullOrWhiteSpace(unit) || (!unit.Equals("Seconds", StringComparison.OrdinalIgnoreCase) &&
                    !unit.Equals("Minutes", StringComparison.OrdinalIgnoreCase) &&
                    !unit.Equals("Hours", StringComparison.OrdinalIgnoreCase) &&
                    !unit.Equals("Days", StringComparison.OrdinalIgnoreCase)))
                {
                    package.TrialDurationUnit = "Days";
                }

                if (package.TrialDurationUnit.Equals("Days", StringComparison.OrdinalIgnoreCase))
                {
                    package.TrialDays = package.TrialDuration;
                }
                else
                {
                    package.TrialDays = (int)Math.Max(1, Math.Ceiling(package.GetTrialTimeSpan().TotalDays));
                }
            }
            else
            {
                package.TrialDuration = 0;
                package.TrialDays = 0;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();
                return View(package);
            }

            package.EnabledFeatures = selectedFeatures ?? new List<string>();
            package.EnabledModules = package.EnabledFeatures;
            package.CreatedAt = DateTime.UtcNow;

            await _licenseService.CreateOrUpdatePackageAsync(package);

            TempData["ToastMessage"] = $"SaaS Package '{package.Name}' created successfully with {package.EnabledFeatures.Count} enabled features!";
            TempData["ToastType"] = "success";

            if (package.IsCustom || !string.IsNullOrEmpty(package.AssignedTenantId) || package.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(CustomPackages));
            }
            return RedirectToAction(nameof(Packages));
        }

        [HttpGet]
        public async Task<IActionResult> EditPackage(string id)
        {
            var package = await _context.SubscriptionPackages.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (package == null) return NotFound();

            ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();
            ViewBag.Tenants = await _context.Tenants.Find(_ => true).ToListAsync();
            return View(package);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPackage(SubscriptionPackage package, List<string> selectedFeatures)
        {
            if (string.IsNullOrWhiteSpace(package.Name))
            {
                ModelState.AddModelError("Name", "Package Name is required.");
            }

            if (package.HasTrial)
            {
                if (package.TrialDuration <= 0)
                {
                    ModelState.AddModelError("TrialDuration", "Trial Duration must be a positive number when free trials are enabled.");
                }
                else if (package.TrialDuration > 100000)
                {
                    ModelState.AddModelError("TrialDuration", "Trial Duration cannot exceed 100,000.");
                }

                var unit = package.TrialDurationUnit?.Trim();
                if (string.IsNullOrWhiteSpace(unit) || (!unit.Equals("Seconds", StringComparison.OrdinalIgnoreCase) &&
                    !unit.Equals("Minutes", StringComparison.OrdinalIgnoreCase) &&
                    !unit.Equals("Hours", StringComparison.OrdinalIgnoreCase) &&
                    !unit.Equals("Days", StringComparison.OrdinalIgnoreCase)))
                {
                    package.TrialDurationUnit = "Days";
                }

                if (package.TrialDurationUnit.Equals("Days", StringComparison.OrdinalIgnoreCase))
                {
                    package.TrialDays = package.TrialDuration;
                }
                else
                {
                    package.TrialDays = (int)Math.Max(1, Math.Ceiling(package.GetTrialTimeSpan().TotalDays));
                }
            }
            else
            {
                package.TrialDuration = 0;
                package.TrialDays = 0;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();
                ViewBag.Tenants = await _context.Tenants.Find(_ => true).ToListAsync();
                return View(package);
            }

            var existing = await _context.SubscriptionPackages.Find(p => p.Id == package.Id).FirstOrDefaultAsync();
            if (existing != null)
            {
                // Guarantee custom package persistence
                if (existing.IsCustom || !string.IsNullOrEmpty(existing.AssignedTenantId) || existing.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase))
                {
                    package.IsCustom = true;
                    if (string.IsNullOrEmpty(package.AssignedTenantId))
                    {
                        package.AssignedTenantId = existing.AssignedTenantId;
                    }
                }
            }

            package.EnabledFeatures = selectedFeatures ?? new List<string>();
            package.EnabledModules = package.EnabledFeatures;

            await _licenseService.CreateOrUpdatePackageAsync(package);

            TempData["ToastMessage"] = $"Package '{package.Name}' updated successfully!";
            TempData["ToastType"] = "success";

            if (package.IsCustom || !string.IsNullOrEmpty(package.AssignedTenantId) || package.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(CustomPackages));
            }

            return RedirectToAction(nameof(Packages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePackage(SubscriptionPackage package, List<string> selectedFeatures, string enabledModulesCsv)
        {
            if (package.HasTrial)
            {
                if (package.TrialDuration <= 0) package.TrialDuration = 14;
                if (string.IsNullOrWhiteSpace(package.TrialDurationUnit)) package.TrialDurationUnit = "Days";
            }

            if (selectedFeatures != null && selectedFeatures.Any())
            {
                package.EnabledFeatures = selectedFeatures;
                package.EnabledModules = selectedFeatures;
            }
            else if (!string.IsNullOrWhiteSpace(enabledModulesCsv))
            {
                package.EnabledFeatures = enabledModulesCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                package.EnabledModules = package.EnabledFeatures;
            }

            var existing = await _context.SubscriptionPackages.Find(p => p.Id == package.Id).FirstOrDefaultAsync();
            if (existing != null && (existing.IsCustom || !string.IsNullOrEmpty(existing.AssignedTenantId) || existing.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase)))
            {
                package.IsCustom = true;
                if (string.IsNullOrEmpty(package.AssignedTenantId))
                {
                    package.AssignedTenantId = existing.AssignedTenantId;
                }
            }

            await _licenseService.CreateOrUpdatePackageAsync(package);
            TempData["ToastMessage"] = $"Subscription Package '{package.Name}' saved successfully.";
            TempData["ToastType"] = "success";

            if (package.IsCustom || !string.IsNullOrEmpty(package.AssignedTenantId) || package.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(CustomPackages));
            }
            return RedirectToAction(nameof(Packages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuplicatePackage(string id)
        {
            var source = await _context.SubscriptionPackages.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (source == null) return NotFound();

            var isCustom = source.IsCustom || !string.IsNullOrEmpty(source.AssignedTenantId) || source.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase);

            var clone = new SubscriptionPackage
            {
                Name = $"{source.Name} (Copy)",
                Description = source.Description,
                MonthlyPrice = source.MonthlyPrice,
                YearlyPrice = source.YearlyPrice,
                TrialDays = source.TrialDays,
                BillingCycle = source.BillingCycle,
                MaxAdmins = source.MaxAdmins,
                MaxEmployees = source.MaxEmployees,
                MaxProducts = source.MaxProducts,
                MaxSuppliers = source.MaxSuppliers,
                EnabledFeatures = new List<string>(source.EnabledFeatures ?? new List<string>()),
                EnabledModules = new List<string>(source.EnabledModules ?? new List<string>()),
                IsActive = true,
                IsFeatured = false,
                IsCustom = isCustom,
                AssignedTenantId = source.AssignedTenantId,
                DisplayOrder = source.DisplayOrder + 1,
                CreatedAt = DateTime.UtcNow
            };

            await _context.SubscriptionPackages.InsertOneAsync(clone);

            TempData["ToastMessage"] = $"Duplicated package '{source.Name}' into '{clone.Name}'.";
            TempData["ToastType"] = "info";

            if (isCustom)
            {
                return RedirectToAction(nameof(CustomPackages));
            }
            return RedirectToAction(nameof(Packages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePackageActive(string id, string? returnUrl = null)
        {
            var pkg = await _context.SubscriptionPackages.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (pkg != null)
            {
                pkg.IsActive = !pkg.IsActive;
                await _context.SubscriptionPackages.ReplaceOneAsync(p => p.Id == id, pkg);
                TempData["ToastMessage"] = $"Package '{pkg.Name}' is now {(pkg.IsActive ? "Active" : "Inactive")}.";
                TempData["ToastType"] = "info";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var isCustom = pkg != null && (pkg.IsCustom || !string.IsNullOrEmpty(pkg.AssignedTenantId) || pkg.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase));
            return isCustom ? RedirectToAction(nameof(CustomPackages)) : RedirectToAction(nameof(Packages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePackage(string id, string? returnUrl = null)
        {
            var pkg = await _context.SubscriptionPackages.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (pkg == null)
            {
                TempData["ToastMessage"] = "Package not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Packages));
            }

            var isCustom = pkg.IsCustom || !string.IsNullOrEmpty(pkg.AssignedTenantId) || pkg.Name.Contains("Custom", StringComparison.OrdinalIgnoreCase);

            var tenantCount = await _context.Tenants.CountDocumentsAsync(t => t.PackageId == id);
            if (tenantCount > 0)
            {
                TempData["ToastMessage"] = $"Cannot delete package: {tenantCount} registered shop(s) are currently subscribed to this package.";
                TempData["ToastType"] = "danger";
                return isCustom ? RedirectToAction(nameof(CustomPackages)) : RedirectToAction(nameof(Packages));
            }

            await _context.SubscriptionPackages.DeleteOneAsync(p => p.Id == id);
            TempData["ToastMessage"] = $"Package '{pkg.Name}' deleted successfully.";
            TempData["ToastType"] = "success";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return isCustom ? RedirectToAction(nameof(CustomPackages)) : RedirectToAction(nameof(Packages));
        }

        [HttpGet]
        public async Task<IActionResult> Payments()
        {
            var transactions = await _context.SaaSPaymentTransactions
                .Find(_ => true)
                .SortByDescending(p => p.PaymentDate)
                .ToListAsync();

            var tenants = await _context.Tenants.Find(_ => true).ToListAsync();
            ViewBag.Tenants = tenants.ToDictionary(t => t.Id, t => t.ShopName);

            return View(transactions);
        }

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var settings = await _platformSettingsService.GetSettingsAsync();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(
            PlatformSettings model,
            List<string>? maintenancePages,
            Microsoft.AspNetCore.Http.IFormFile? logoFile)
        {
            if (logoFile != null && logoFile.Length > 0)
            {
                try
                {
                    var uploadResult = await _imageService.UploadImageAsync(logoFile, "platform_branding");
                    if (uploadResult != null && uploadResult.IsSuccess && !string.IsNullOrWhiteSpace(uploadResult.SecureUrl))
                    {
                        model.LogoUrl = uploadResult.SecureUrl;
                    }
                }
                catch (Exception)
                {
                    // keep existing LogoUrl if upload fails
                }
            }

            model.MaintenancePages = maintenancePages ?? new List<string>();

            await _platformSettingsService.SaveSettingsAsync(model, User.Identity?.Name ?? "SuperAdmin");
            await _auditLogService.LogActivityAsync("Platform Settings Updated", User.Identity?.Name ?? "SuperAdmin", "SaaS Platform Config", "Updated SaaS branding, contact details, and maintenance schedule.");

            TempData["ToastMessage"] = "SaaS Platform branding and maintenance configurations saved successfully!";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMaintenance(string target, bool enabled)
        {
            var settings = await _platformSettingsService.GetSettingsAsync();
            if (target.Equals("website", StringComparison.OrdinalIgnoreCase))
            {
                settings.WebsiteMaintenanceEnabled = enabled;
            }
            else if (target.Equals("shop", StringComparison.OrdinalIgnoreCase))
            {
                settings.ShopDashboardMaintenanceEnabled = enabled;
            }

            await _platformSettingsService.SaveSettingsAsync(settings, User.Identity?.Name ?? "SuperAdmin");
            await _auditLogService.LogActivityAsync("Maintenance Mode Toggled", User.Identity?.Name ?? "SuperAdmin", "SaaS Maintenance", $"Toggled {target} maintenance mode to {enabled}.");

            TempData["ToastMessage"] = $"{(target.Equals("website", StringComparison.OrdinalIgnoreCase) ? "Website" : "Shop Dashboard")} maintenance mode {(enabled ? "activated" : "deactivated")} successfully!";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Settings));
        }

        [HttpGet]
        public async Task<IActionResult> RenewalRequests(string? status, string? search)
        {
            var filterBuilder = Builders<LicenseRenewalRequest>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrWhiteSpace(status))
            {
                filter &= filterBuilder.Eq(r => r.Status, status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var regex = new MongoDB.Bson.BsonRegularExpression(search.Trim(), "i");
                filter &= filterBuilder.Or(
                    filterBuilder.Regex(r => r.ShopName, regex),
                    filterBuilder.Regex(r => r.TenantCode, regex),
                    filterBuilder.Regex(r => r.OwnerName, regex),
                    filterBuilder.Regex(r => r.PackageName, regex),
                    filterBuilder.Regex(r => r.RequestMessage, regex)
                );
            }

            var requests = await _context.LicenseRenewalRequests.Find(filter).SortByDescending(r => r.RequestedAt).ToListAsync();
            var allRequests = await _context.LicenseRenewalRequests.Find(_ => true).ToListAsync();

            ViewBag.PendingCount = allRequests.Count(r => r.Status == "Pending");
            ViewBag.ApprovedCount = allRequests.Count(r => r.Status == "Approved");
            ViewBag.RejectedCount = allRequests.Count(r => r.Status == "Rejected");
            ViewBag.TotalCount = allRequests.Count;
            ViewBag.CurrentStatus = status;
            ViewBag.Search = search;

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRenewalRequest(string requestId, int extensionDays, string? adminNotes, string? returnUrl)
        {
            var request = await _context.LicenseRenewalRequests.Find(r => r.Id == requestId).FirstOrDefaultAsync();
            if (request == null)
            {
                TempData["ToastMessage"] = "Renewal request not found.";
                TempData["ToastType"] = "danger";
                return Redirect(returnUrl ?? Url.Action(nameof(Dashboard)));
            }

            var tenant = await _context.Tenants.Find(t => t.Id == request.TenantId).FirstOrDefaultAsync();
            if (tenant == null)
            {
                TempData["ToastMessage"] = $"Associated tenant shop ({request.TenantId}) not found.";
                TempData["ToastType"] = "danger";
                return Redirect(returnUrl ?? Url.Action(nameof(Dashboard)));
            }

            var license = await _licenseService.GetActiveLicenseAsync(tenant.Id);
            if (extensionDays <= 0)
            {
                extensionDays = request.RequestedDays > 0 ? request.RequestedDays : 30;
            }

            DateTime baseDate = (license != null && license.ExpiryDate > DateTime.UtcNow) 
                ? license.ExpiryDate 
                : DateTime.UtcNow;

            DateTime newExpiry = baseDate.AddDays(extensionDays);

            // Update or Create License
            if (license != null)
            {
                license.ExpiryDate = newExpiry;
                license.Status = "Active";
                if (!string.IsNullOrEmpty(request.PackageId))
                {
                    license.PackageId = request.PackageId;
                }
                await _context.TenantLicenses.ReplaceOneAsync(l => l.Id == license.Id, license);
            }
            else
            {
                var newLic = new TenantLicense
                {
                    TenantId = tenant.Id,
                    PackageId = !string.IsNullOrEmpty(request.PackageId) ? request.PackageId : tenant.PackageId,
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = newExpiry,
                    Status = "Active",
                    AutoRenew = false
                };
                await _context.TenantLicenses.InsertOneAsync(newLic);
            }

            // Sync Tenant status and package
            tenant.Status = "Active";
            if (!string.IsNullOrEmpty(request.PackageId))
            {
                tenant.PackageId = request.PackageId;
            }
            await _context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);

            // Mark renewal request as Approved
            request.Status = "Approved";
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedBy = User.Identity?.Name ?? "SuperAdmin";
            request.AdminNotes = adminNotes?.Trim();
            await _context.LicenseRenewalRequests.ReplaceOneAsync(r => r.Id == request.Id, request);

            // Record SaaS Transaction
            string refSuffix = !string.IsNullOrEmpty(request.Id) && request.Id.Length >= 8
                ? request.Id[..8].ToUpper()
                : (request.Id ?? Guid.NewGuid().ToString("N")[..8]).ToUpper();

            var payment = new SaaSPaymentTransaction
            {
                TenantId = tenant.Id,
                PackageId = request.PackageId,
                Amount = request.PackageMonthlyPrice * (request.RequestedDurationMonths > 0 ? request.RequestedDurationMonths : 1),
                Currency = "INR",
                Status = "Success",
                GatewayProvider = !string.IsNullOrWhiteSpace(request.PaymentModePreference) ? request.PaymentModePreference : "Manual / Admin Approval",
                PaymentDate = DateTime.UtcNow,
                TransactionRef = $"RENEW-{refSuffix}"
            };
            await _context.SaaSPaymentTransactions.InsertOneAsync(payment);

            // Audit Log
            await _auditLogService.LogActivityAsync(
                "RENEWAL_REQUEST_APPROVED",
                User.Identity?.Name ?? "SuperAdmin",
                $"Shop: {tenant.ShopName} ({tenant.TenantCode})",
                $"Approved renewal request for package '{request.PackageName}'. Extended license by {extensionDays} days to {newExpiry.ToIstString("dd-MMM-yyyy hh:mm:ss tt IST")}.");

            // Send notification email to shop
            try
            {
                var recipientEmail = !string.IsNullOrWhiteSpace(request.ContactEmail) ? request.ContactEmail : tenant.ContactEmail;
                if (!string.IsNullOrWhiteSpace(recipientEmail))
                {
                    await _emailService.SendLicenseRenewalApprovedEmailAsync(
                        recipientEmail,
                        tenant.ShopName,
                        tenant.OwnerName,
                        request.PackageName,
                        extensionDays,
                        newExpiry,
                        adminNotes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to send renewal approval email to {request.ContactEmail}: {ex.Message}");
            }

            TempData["ToastMessage"] = $"Renewal for '{tenant.ShopName}' approved! License extended by {extensionDays} days (New Expiry: {newExpiry:dd MMM yyyy}). Confirmation email sent.";
            TempData["ToastType"] = "success";

            return Redirect(returnUrl ?? Url.Action(nameof(Dashboard)) ?? "/PlatformAdmin/Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRenewalRequest(string requestId, string? adminNotes, string? returnUrl)
        {
            var request = await _context.LicenseRenewalRequests.Find(r => r.Id == requestId).FirstOrDefaultAsync();
            if (request == null)
            {
                TempData["ToastMessage"] = "Renewal request not found.";
                TempData["ToastType"] = "danger";
                return Redirect(returnUrl ?? Url.Action(nameof(Dashboard)) ?? "/PlatformAdmin/Dashboard");
            }

            request.Status = "Rejected";
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedBy = User.Identity?.Name ?? "SuperAdmin";
            request.AdminNotes = adminNotes?.Trim();
            await _context.LicenseRenewalRequests.ReplaceOneAsync(r => r.Id == request.Id, request);

            await _auditLogService.LogActivityAsync(
                "RENEWAL_REQUEST_REJECTED",
                User.Identity?.Name ?? "SuperAdmin",
                $"Shop: {request.ShopName} ({request.TenantCode})",
                $"Rejected renewal request for package '{request.PackageName}'. Reason: {adminNotes}");

            // Send notification email to shop
            try
            {
                var tenant = await _context.Tenants.Find(t => t.Id == request.TenantId).FirstOrDefaultAsync();
                var recipientEmail = !string.IsNullOrWhiteSpace(request.ContactEmail) ? request.ContactEmail : tenant?.ContactEmail;
                if (!string.IsNullOrWhiteSpace(recipientEmail))
                {
                    await _emailService.SendLicenseRenewalRejectedEmailAsync(
                        recipientEmail,
                        request.ShopName,
                        request.OwnerName,
                        request.PackageName,
                        adminNotes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to send renewal rejection email to {request.ContactEmail}: {ex.Message}");
            }

            TempData["ToastMessage"] = $"Renewal request for '{request.ShopName}' has been rejected. Notification email sent to shop.";
            TempData["ToastType"] = "warning";

            return Redirect(returnUrl ?? Url.Action(nameof(Dashboard)) ?? "/PlatformAdmin/Dashboard");
        }

        [HttpGet]
        [Route("platform/RenewalRequestDetails/{id}")]
        public async Task<IActionResult> RenewalRequestDetails(string id)
        {
            var request = await _context.LicenseRenewalRequests.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (request == null)
            {
                TempData["ToastMessage"] = "Renewal request not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(RenewalRequests));
            }

            var tenant = await _context.Tenants.Find(t => t.Id == request.TenantId).FirstOrDefaultAsync();
            var license = tenant != null ? await _licenseService.GetActiveLicenseAsync(tenant.Id) : null;
            var currentPackage = tenant != null && !string.IsNullOrEmpty(tenant.PackageId)
                ? await _context.SubscriptionPackages.Find(p => p.Id == tenant.PackageId).FirstOrDefaultAsync()
                : null;

            ViewBag.Tenant = tenant;
            ViewBag.License = license;
            ViewBag.CurrentPackage = currentPackage;

            return View(request);
        }

        #region Custom Pricing Configuration

        [HttpGet]
        public async Task<IActionResult> CustomPricing()
        {
            var config = await _customPricingService.GetConfigurationAsync();
            var allFeatures = await _featureCatalogService.GetActiveFeaturesAsync();
            var groupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();

            ViewBag.AllFeatures = allFeatures;
            ViewBag.GroupedFeatures = groupedFeatures;
            return View(config);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCustomPricing(
            CustomPricingConfiguration config,
            List<string>? featureKey,
            List<decimal>? featurePrice,
            List<string>? tierName,
            List<int>? tierMin,
            List<int>? tierMax,
            List<decimal>? tierFlat,
            List<decimal>? tierPerUnit)
        {
            var existing = await _customPricingService.GetConfigurationAsync();

            existing.BaseMonthlyPrice = Math.Max(0, config.BaseMonthlyPrice);
            existing.AdminUnitPrice = Math.Max(0, config.AdminUnitPrice);
            existing.EmployeeUnitPrice = Math.Max(0, config.EmployeeUnitPrice);
            existing.ProductPricingModel = string.Equals(config.ProductPricingModel, "PerUnit", StringComparison.OrdinalIgnoreCase) ? "PerUnit" : "Tiered";
            existing.ProductUnitPrice = Math.Max(0, config.ProductUnitPrice);
            existing.SupplierUnitPrice = Math.Max(0, config.SupplierUnitPrice);
            existing.DeviceUnitPrice = 0m;
            existing.AnnualDiscountPercent = Math.Clamp(config.AnnualDiscountPercent, 0, 100);

            existing.MinAdmins = Math.Max(1, config.MinAdmins);
            existing.MaxAdmins = Math.Max(existing.MinAdmins, config.MaxAdmins);
            existing.MinEmployees = Math.Max(1, config.MinEmployees);
            existing.MaxEmployees = Math.Max(existing.MinEmployees, config.MaxEmployees);
            existing.MinProducts = Math.Max(0, config.MinProducts);
            existing.MaxProducts = Math.Max(existing.MinProducts, config.MaxProducts);
            existing.MinSuppliers = Math.Max(0, config.MinSuppliers);
            existing.MaxSuppliers = Math.Max(existing.MinSuppliers, config.MaxSuppliers);

            existing.IsEnabled = config.IsEnabled;
            existing.AllowCustomQuotes = config.AllowCustomQuotes;

            // Reconstruct product tiers
            if (tierName != null && tierName.Count > 0)
            {
                var newTiers = new List<ProductTierPricing>();
                for (int i = 0; i < tierName.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(tierName[i])) continue;
                    newTiers.Add(new ProductTierPricing
                    {
                        TierName = tierName[i].Trim(),
                        MinProducts = tierMin != null && i < tierMin.Count ? tierMin[i] : 0,
                        MaxProducts = tierMax != null && i < tierMax.Count ? tierMax[i] : 1000,
                        FlatPrice = tierFlat != null && i < tierFlat.Count ? Math.Max(0, tierFlat[i]) : 0,
                        PerUnitRate = tierPerUnit != null && i < tierPerUnit.Count ? Math.Max(0, tierPerUnit[i]) : 0
                    });
                }
                if (newTiers.Any())
                {
                    existing.ProductTiers = newTiers;
                }
            }

            // Reconstruct feature extra charges
            existing.FeatureRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (featureKey != null && featurePrice != null)
            {
                for (int i = 0; i < featureKey.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(featureKey[i]) && i < featurePrice.Count && featurePrice[i] > 0)
                    {
                        existing.FeatureRates[featureKey[i].Trim()] = featurePrice[i];
                    }
                }
            }

            await _customPricingService.SaveConfigurationAsync(existing, User.Identity?.Name ?? "SuperAdmin");
            await _auditLogService.LogActivityAsync("CUSTOM_PRICING_CONFIG_UPDATED", User.Identity?.Name ?? "SuperAdmin", existing.Id, "Updated SaaS custom package pricing rules and tier boundaries.");

            TempData["ToastMessage"] = "Custom package pricing rules saved successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(CustomPricing));
        }

        #endregion

        #region Custom Package Inquiries

        [HttpGet]
        public async Task<IActionResult> CustomPackageInquiries(string? search = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var inquiries = await _customPricingService.GetInquiriesAsync(search, status, fromDate, toDate);
            var allInquiries = await _customPricingService.GetInquiriesAsync(null, null, null, null);

            ViewBag.TotalCount = allInquiries.Count;
            ViewBag.NewCount = allInquiries.Count(i => i.Status == "New");
            ViewBag.InReviewCount = allInquiries.Count(i => i.Status == "In Review");
            ViewBag.QuoteSentCount = allInquiries.Count(i => i.Status == "Quote Sent");
            ViewBag.ConvertedCount = allInquiries.Count(i => i.Status == "Converted");
            ViewBag.ClosedCount = allInquiries.Count(i => i.Status == "Closed");

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status ?? "All";
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(inquiries);
        }

        [HttpGet]
        public async Task<IActionResult> CustomPackageInquiryDetails(string id)
        {
            var inquiry = await _customPricingService.GetInquiryByIdAsync(id);
            if (inquiry == null) return NotFound();

            return Json(new
            {
                id = inquiry.Id,
                inquiryNumber = inquiry.InquiryNumber,
                contactName = inquiry.ContactName,
                businessName = inquiry.BusinessName,
                email = inquiry.Email,
                phone = inquiry.Phone,
                message = inquiry.Message,
                adminCount = inquiry.AdminCount,
                employeeCount = inquiry.EmployeeCount,
                productCount = inquiry.ProductCount,
                supplierCount = inquiry.SupplierCount,
                deviceCount = inquiry.DeviceCount,
                selectedFeatures = inquiry.SelectedFeatureNames,
                selectedFeatureKeys = inquiry.SelectedFeatureKeys,
                monthlyPrice = inquiry.EstimatedMonthlyPrice,
                yearlyPrice = inquiry.EstimatedYearlyPrice,
                snapshotJson = inquiry.PricingConfigSnapshotJson,
                status = inquiry.Status,
                confirmationEmailStatus = inquiry.ConfirmationEmailStatus,
                confirmationEmailSentAt = inquiry.ConfirmationEmailSentAt?.ToIstString("dd MMM yyyy, hh:mm tt IST"),
                confirmationEmailError = inquiry.ConfirmationEmailError,
                adminNotes = inquiry.AdminNotes,
                history = inquiry.FollowUpHistory?.Select(h => new
                {
                    note = h.Note,
                    createdBy = h.CreatedBy,
                    timestamp = h.Timestamp.ToIstString("dd MMM yyyy, hh:mm tt IST"),
                    statusChangedTo = h.StatusChangedTo
                }),
                createdAt = inquiry.CreatedAt.ToIstString("dd MMM yyyy, hh:mm tt IST"),
                updatedAt = inquiry.UpdatedAt.ToIstString("dd MMM yyyy, hh:mm tt IST")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCustomPackageInquiryStatus(string id, string status, string? note, string? convertedTenantId = null, bool sendEmailToCustomer = false)
        {
            var inquiry = await _customPricingService.GetInquiryByIdAsync(id);
            if (inquiry == null)
            {
                TempData["ToastMessage"] = "Inquiry not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(CustomPackageInquiries));
            }

            var noteToSave = note;
            if (sendEmailToCustomer && !string.IsNullOrWhiteSpace(note))
            {
                noteToSave = $"[Email Reply Sent to {inquiry.Email}] {note.Trim()}";
            }

            var success = await _customPricingService.UpdateInquiryStatusAsync(id, status, noteToSave, User.Identity?.Name ?? "SuperAdmin", convertedTenantId);
            if (!success)
            {
                TempData["ToastMessage"] = "Inquiry not found or could not be updated.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(CustomPackageInquiries));
            }

            await _auditLogService.LogActivityAsync("CUSTOM_PACKAGE_INQUIRY_STATUS_UPDATED", User.Identity?.Name ?? "SuperAdmin", id, $"Status changed to {status}. Note: {note}");

            if (sendEmailToCustomer && !string.IsNullOrWhiteSpace(note))
            {
                var (emailSent, emailErr) = await _emailService.SendCustomQuoteReplyEmailAsync(
                    inquiry.Email,
                    inquiry.ContactName,
                    inquiry.InquiryNumber,
                    status,
                    note.Trim());

                if (emailSent)
                {
                    TempData["ToastMessage"] = $"Inquiry updated and email reply sent successfully to {inquiry.Email}.";
                    TempData["ToastType"] = "success";
                }
                else
                {
                    TempData["ToastMessage"] = $"Inquiry status updated, but email sending to {inquiry.Email} failed: {emailErr}";
                    TempData["ToastType"] = "warning";
                }
            }
            else
            {
                TempData["ToastMessage"] = $"Inquiry status successfully updated to '{status}'.";
                TempData["ToastType"] = "success";
            }

            return RedirectToAction(nameof(CustomPackageInquiries));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryCustomQuoteConfirmationEmail(string id)
        {
            var inquiry = await _customPricingService.GetInquiryByIdAsync(id);
            if (inquiry == null)
            {
                TempData["ToastMessage"] = "Inquiry not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(CustomPackageInquiries));
            }

            var (success, errorMsg) = await _emailService.SendCustomQuoteConfirmationEmailAsync(
                inquiry.Email,
                inquiry.ContactName,
                inquiry.InquiryNumber);

            await _customPricingService.UpdateConfirmationEmailStatusAsync(
                inquiry.Id,
                success ? "Sent" : "Failed",
                errorMsg);

            await _auditLogService.LogActivityAsync(
                "CUSTOM_QUOTE_EMAIL_RETRY",
                User.Identity?.Name ?? "SuperAdmin",
                inquiry.Id,
                $"Retry result: {(success ? "Success" : "Failed - " + errorMsg)}");

            if (success)
            {
                TempData["ToastMessage"] = $"Confirmation email successfully resent to {inquiry.Email}.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = $"Confirmation email failed to send: {errorMsg}";
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(CustomPackageInquiries));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomPackageInquiry(string id)
        {
            var (success, message) = await _customPricingService.DeleteInquiryAsync(id, User.Identity?.Name ?? "SuperAdmin");
            if (success)
            {
                await _auditLogService.LogActivityAsync("CUSTOM_PACKAGE_INQUIRY_DELETED", User.Identity?.Name ?? "SuperAdmin", id, message);
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(CustomPackageInquiries));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDeleteCustomPackageInquiries([FromForm] List<string> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["ToastMessage"] = "No inquiries selected for deletion.";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(CustomPackageInquiries));
            }

            var (deletedCount, message) = await _customPricingService.BulkDeleteInquiriesAsync(selectedIds, User.Identity?.Name ?? "SuperAdmin");
            if (deletedCount > 0)
            {
                await _auditLogService.LogActivityAsync("CUSTOM_PACKAGE_INQUIRIES_BULK_DELETED", User.Identity?.Name ?? "SuperAdmin", $"{deletedCount} deleted", message);
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(CustomPackageInquiries));
        }

        #endregion

        #region Contact Inquiries

        [HttpGet]
        public async Task<IActionResult> ContactInquiries(string? search = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var inquiries = await _contactInquiryService.GetInquiriesAsync(search, status, fromDate, toDate);
            var allInquiries = await _contactInquiryService.GetInquiriesAsync(null, null, null, null);

            ViewBag.TotalCount = allInquiries.Count;
            ViewBag.NewCount = allInquiries.Count(i => i.Status == "New");
            ViewBag.InProgressCount = allInquiries.Count(i => i.Status == "In Progress");
            ViewBag.RespondedCount = allInquiries.Count(i => i.Status == "Responded");
            ViewBag.ResolvedCount = allInquiries.Count(i => i.Status == "Resolved");
            ViewBag.ClosedCount = allInquiries.Count(i => i.Status == "Closed");

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status ?? "All";
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(inquiries);
        }

        [HttpGet]
        public async Task<IActionResult> ContactInquiryDetails(string id)
        {
            var inquiry = await _contactInquiryService.GetInquiryByIdAsync(id);
            if (inquiry == null) return NotFound();

            return Json(new
            {
                id = inquiry.Id,
                referenceNumber = inquiry.ReferenceNumber,
                fullName = inquiry.FullName,
                businessName = inquiry.BusinessName,
                email = inquiry.Email,
                phone = inquiry.Phone,
                subject = inquiry.Subject,
                message = inquiry.Message,
                status = inquiry.Status,
                confirmationEmailStatus = inquiry.ConfirmationEmailStatus,
                confirmationEmailSentAt = inquiry.ConfirmationEmailSentAt?.ToIstString("dd MMM yyyy, hh:mm tt IST"),
                confirmationEmailError = inquiry.ConfirmationEmailError,
                adminNotes = inquiry.AdminNotes,
                history = inquiry.FollowUpHistory?.Select(h => new
                {
                    note = h.Note,
                    createdBy = h.CreatedBy,
                    timestamp = h.Timestamp.ToIstString("dd MMM yyyy, hh:mm tt IST"),
                    statusChangedTo = h.StatusChangedTo
                }),
                createdAt = inquiry.CreatedAt.ToIstString("dd MMM yyyy, hh:mm tt IST"),
                updatedAt = inquiry.UpdatedAt.ToIstString("dd MMM yyyy, hh:mm tt IST")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateContactInquiryStatus(string id, string status, string? note, bool sendEmailToCustomer = false)
        {
            var inquiry = await _contactInquiryService.GetInquiryByIdAsync(id);
            if (inquiry == null)
            {
                TempData["ToastMessage"] = "Contact inquiry not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(ContactInquiries));
            }

            var noteToSave = note;
            if (sendEmailToCustomer && !string.IsNullOrWhiteSpace(note))
            {
                noteToSave = $"[Email Reply Sent to {inquiry.Email}] {note.Trim()}";
            }

            var success = await _contactInquiryService.UpdateInquiryStatusAsync(id, status, noteToSave, User.Identity?.Name ?? "SuperAdmin");
            if (!success)
            {
                TempData["ToastMessage"] = "Contact inquiry not found or could not be updated.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(ContactInquiries));
            }

            await _auditLogService.LogActivityAsync("CONTACT_INQUIRY_STATUS_UPDATED", User.Identity?.Name ?? "SuperAdmin", id, $"Status changed to {status}. Note: {note}");

            if (sendEmailToCustomer && !string.IsNullOrWhiteSpace(note))
            {
                var (emailSent, emailErr) = await _emailService.SendContactInquiryReplyEmailAsync(
                    inquiry.Email,
                    inquiry.FullName,
                    inquiry.ReferenceNumber,
                    status,
                    note.Trim());

                if (emailSent)
                {
                    TempData["ToastMessage"] = $"Contact inquiry updated and email reply sent successfully to {inquiry.Email}.";
                    TempData["ToastType"] = "success";
                }
                else
                {
                    TempData["ToastMessage"] = $"Status updated, but email sending to {inquiry.Email} failed: {emailErr}";
                    TempData["ToastType"] = "warning";
                }
            }
            else
            {
                TempData["ToastMessage"] = $"Contact inquiry status successfully updated to '{status}'.";
                TempData["ToastType"] = "success";
            }

            return RedirectToAction(nameof(ContactInquiries));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryContactInquiryConfirmationEmail(string id)
        {
            var inquiry = await _contactInquiryService.GetInquiryByIdAsync(id);
            if (inquiry == null)
            {
                TempData["ToastMessage"] = "Inquiry not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(ContactInquiries));
            }

            var (success, errorMsg) = await _emailService.SendContactInquiryConfirmationEmailAsync(
                inquiry.Email,
                inquiry.FullName,
                inquiry.ReferenceNumber);

            await _contactInquiryService.UpdateConfirmationEmailStatusAsync(
                inquiry.Id,
                success ? "Sent" : "Failed",
                errorMsg);

            await _auditLogService.LogActivityAsync(
                "CONTACT_INQUIRY_EMAIL_RETRY",
                User.Identity?.Name ?? "SuperAdmin",
                inquiry.Id,
                $"Retry result: {(success ? "Success" : "Failed - " + errorMsg)}");

            if (success)
            {
                TempData["ToastMessage"] = $"Confirmation email successfully resent to {inquiry.Email}.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = $"Confirmation email failed to send: {errorMsg}";
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(ContactInquiries));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteContactInquiry(string id)
        {
            var (success, message) = await _contactInquiryService.DeleteInquiryAsync(id, User.Identity?.Name ?? "SuperAdmin");
            if (success)
            {
                await _auditLogService.LogActivityAsync("CONTACT_INQUIRY_DELETED", User.Identity?.Name ?? "SuperAdmin", id, message);
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(ContactInquiries));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDeleteContactInquiries([FromForm] List<string> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["ToastMessage"] = "No contact messages selected for deletion.";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(ContactInquiries));
            }

            var (deletedCount, message) = await _contactInquiryService.BulkDeleteInquiriesAsync(selectedIds, User.Identity?.Name ?? "SuperAdmin");
            if (deletedCount > 0)
            {
                await _auditLogService.LogActivityAsync("CONTACT_INQUIRIES_BULK_DELETED", User.Identity?.Name ?? "SuperAdmin", $"{deletedCount} deleted", message);
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(ContactInquiries));
        }

        #endregion

        #region Website Policy Management CMS

        [HttpGet]
        public async Task<IActionResult> Policies()
        {
            var policies = await _policyService.GetAllPoliciesAsync(includeInactive: true);
            return View(policies);
        }

        [HttpGet]
        public IActionResult CreatePolicy()
        {
            var model = new PolicyDocument
            {
                EffectiveDate = DateTime.UtcNow,
                IsPublished = true,
                IsActive = true,
                ShowInFooter = true,
                DisplayOrder = 1
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePolicy(PolicyDocument model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("Title", "Policy Title is required.");
            }
            if (string.IsNullOrWhiteSpace(model.ContentMarkdown))
            {
                ModelState.AddModelError("ContentMarkdown", "Policy content in Markdown format is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var admin = User.Identity?.Name ?? "SuperAdmin";
            var created = await _policyService.CreatePolicyAsync(model, admin);

            await _auditLogService.LogActivityAsync("POLICY_CREATED", admin, created.Id, $"Created policy '{created.Title}' with slug '{created.Slug}'");
            TempData["ToastMessage"] = $"Policy '{created.Title}' created successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Policies));
        }

        [HttpGet]
        public async Task<IActionResult> EditPolicy(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var policy = await _policyService.GetByIdAsync(id);
            if (policy == null) return NotFound();
            return View(policy);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPolicy(string id, PolicyDocument model, string changeNotes = "")
        {
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("Title", "Policy Title is required.");
            }
            if (string.IsNullOrWhiteSpace(model.ContentMarkdown))
            {
                ModelState.AddModelError("ContentMarkdown", "Policy content in Markdown format is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Id = id;
            var admin = User.Identity?.Name ?? "SuperAdmin";
            var updated = await _policyService.UpdatePolicyAsync(model, admin, changeNotes);

            if (updated == null)
            {
                TempData["ToastMessage"] = "Policy not found or could not be updated.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Policies));
            }

            await _auditLogService.LogActivityAsync("POLICY_UPDATED", admin, updated.Id, $"Updated policy '{updated.Title}' ({updated.Slug}). Notes: {changeNotes}");
            TempData["ToastMessage"] = $"Policy '{updated.Title}' updated and revision recorded successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Policies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublishPolicy(string id, bool publish)
        {
            var admin = User.Identity?.Name ?? "SuperAdmin";
            var success = await _policyService.TogglePublishAsync(id, publish, admin);
            if (success)
            {
                await _auditLogService.LogActivityAsync(publish ? "POLICY_PUBLISHED" : "POLICY_UNPUBLISHED", admin, id, $"Publication status set to {publish}");
                TempData["ToastMessage"] = $"Policy publication status updated successfully.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = "Policy not found.";
                TempData["ToastType"] = "danger";
            }
            return RedirectToAction(nameof(Policies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActivePolicy(string id, bool active)
        {
            var admin = User.Identity?.Name ?? "SuperAdmin";
            var success = await _policyService.ToggleActiveAsync(id, active, admin);
            if (success)
            {
                await _auditLogService.LogActivityAsync(active ? "POLICY_ACTIVATED" : "POLICY_DEACTIVATED", admin, id, $"Active status set to {active}");
                TempData["ToastMessage"] = $"Policy active status updated successfully.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = "Policy not found.";
                TempData["ToastType"] = "danger";
            }
            return RedirectToAction(nameof(Policies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePolicy(string id)
        {
            var admin = User.Identity?.Name ?? "SuperAdmin";
            var success = await _policyService.DeletePolicyAsync(id, admin);
            if (success)
            {
                await _auditLogService.LogActivityAsync("POLICY_DELETED", admin, id, "Soft-deleted / archived policy document");
                TempData["ToastMessage"] = "Policy archived and removed from public access successfully.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = "Policy not found or could not be deleted.";
                TempData["ToastType"] = "danger";
            }
            return RedirectToAction(nameof(Policies));
        }

        [HttpGet]
        public async Task<IActionResult> PolicyRevisions(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var policy = await _policyService.GetByIdAsync(id);
            if (policy == null) return NotFound();

            var revisions = await _policyService.GetRevisionsAsync(id);
            ViewBag.Policy = policy;
            return View(revisions);
        }

        [HttpPost]
        public IActionResult PreviewPolicyMarkdown([FromBody] MarkdownPreviewDto dto)
        {
            var safeHtml = _policyService.ConvertMarkdownToSafeHtml(dto?.Markdown ?? string.Empty);
            return Json(new { html = safeHtml });
        }

        #endregion

        #region Free-Trial Approvals & Verification Workflow

        [HttpGet]
        [Route("/PlatformAdmin/PendingApprovals")]
        [Route("/platform/PendingApprovals")]
        public async Task<IActionResult> PendingApprovals(string? search, string? packageId)
        {
            var filterBuilder = Builders<Tenant>.Filter;
            var filter = filterBuilder.Eq(t => t.Status, "PendingApproval");

            if (!string.IsNullOrWhiteSpace(packageId) && packageId != "All")
            {
                filter &= filterBuilder.Eq(t => t.PackageId, packageId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                filter &= (filterBuilder.Regex(t => t.ShopName, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.OwnerName, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.ContactEmail, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.Phone, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.TenantCode, new MongoDB.Bson.BsonRegularExpression(s, "i")));
            }

            var pendingTenants = await _context.Tenants
                .Find(filter)
                .SortByDescending(t => t.CreatedAt)
                .ToListAsync();

            var packages = await _context.SubscriptionPackages.Find(_ => true).ToListAsync();
            var packageMap = packages.ToDictionary(p => p.Id, p => p);

            ViewBag.PackageMap = packageMap;
            ViewBag.Packages = packages;
            ViewBag.CurrentSearch = search ?? string.Empty;
            ViewBag.CurrentPackageId = packageId ?? "All";
            ViewBag.TotalPendingCount = await _context.Tenants.CountDocumentsAsync(t => t.Status == "PendingApproval");

            return View(pendingTenants);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTrialRegistration(string tenantId, string? notes)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                TempData["ToastMessage"] = "Invalid shop identifier.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(PendingApprovals));
            }

            var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
            if (tenant == null)
            {
                TempData["ToastMessage"] = "Shop account not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(PendingApprovals));
            }

            if (!string.Equals(tenant.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ToastMessage"] = $"Cannot approve shop: current status is '{tenant.Status}'.";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(PendingApprovals));
            }

            var adminUser = User.Identity?.Name ?? "SuperAdmin";

            // 1. Activate Trial Subscription with duration starting NOW in IST
            var license = await _licenseService.CreateTrialLicenseFromPackageAsync(tenant.Id, tenant.PackageId);

            // 2. Mark tenant active & record approval metadata
            tenant.Status = "Active";
            tenant.ApprovedBy = adminUser;
            tenant.ApprovedAt = DateTime.UtcNow;
            tenant.ApprovalNotes = notes?.Trim();
            tenant.UpdatedAt = DateTime.UtcNow;
            await _context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);

            // 3. Send Official Approval Email with IST Timestamps
            try
            {
                await _emailService.SendTrialApprovedEmailAsync(
                    tenant.ContactEmail,
                    tenant.OwnerName,
                    license.StartDate,
                    license.ExpiryDate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL NOTICE] Trial approval email error: {ex.Message}");
            }

            // 4. Audit Log
            await _auditLogService.LogActivityAsync(
                "TRIAL_REGISTRATION_APPROVED",
                adminUser,
                $"Shop: {tenant.ShopName} ({tenant.TenantCode})",
                $"Approved free trial registration. Trial clock started. Expiry: {license.ExpiryDate.ToPolicyIstDisplayString()}. Notes: {notes}");

            TempData["ToastMessage"] = $"Shop '{tenant.ShopName}' ({tenant.TenantCode}) approved! Trial started at current time.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(PendingApprovals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTrialRegistration(string tenantId, string reason)
        {
            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(reason))
            {
                TempData["ToastMessage"] = "Please provide a valid rejection reason.";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(PendingApprovals));
            }

            var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
            if (tenant == null)
            {
                TempData["ToastMessage"] = "Shop account not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(PendingApprovals));
            }

            var adminUser = User.Identity?.Name ?? "SuperAdmin";

            tenant.Status = "Rejected";
            tenant.RejectionReason = reason.Trim();
            tenant.RejectedAt = DateTime.UtcNow;
            tenant.ApprovedBy = adminUser;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);

            try
            {
                await _emailService.SendTrialRejectedEmailAsync(
                    tenant.ContactEmail,
                    tenant.OwnerName,
                    reason.Trim());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL NOTICE] Trial rejection email error: {ex.Message}");
            }

            await _auditLogService.LogActivityAsync(
                "TRIAL_REGISTRATION_REJECTED",
                adminUser,
                $"Shop: {tenant.ShopName} ({tenant.TenantCode})",
                $"Rejected free trial registration. Reason: {reason}");

            TempData["ToastMessage"] = $"Registration for '{tenant.ShopName}' rejected.";
            TempData["ToastType"] = "info";

            return RedirectToAction(nameof(PendingApprovals));
        }

        #endregion

        #region Revenue & Payment Management Dashboard

        [HttpGet]
        [Route("/PlatformAdmin/Revenue")]
        [Route("/platform/Revenue")]
        public async Task<IActionResult> Revenue(
            string? status, 
            string? activationStatus, 
            string? billingCycle,
            string? packageId,
            string? search, 
            string? fromDate,
            string? toDate,
            string? tab,
            int page = 1)
        {
            var metrics = await _saasPaymentService.GetRevenueMetricsAsync();
            var platformSettings = await _platformSettingsService.GetSettingsAsync();

            var filterBuilder = Builders<SaaSPaymentTransaction>.Filter;
            var filter = filterBuilder.Empty;

            var activeTab = (tab ?? "all").ToLowerInvariant();

            var retentionSpan = platformSettings.GetTemporaryCheckoutRetentionTimeSpan();
            var cutoffUtc = DateTime.UtcNow.Subtract(retentionSpan);

            // Tab-specific partitioning
            switch (activeTab)
            {
                case "recovery": // Permanent Paid-but-Unactivated Recovery Queue
                    filter &= filterBuilder.Eq(t => t.PaymentStatus, "Paid") 
                           & filterBuilder.Ne(t => t.ActivationStatus, "Activated")
                           & filterBuilder.Ne(t => t.IsManuallyResolved, true);
                    break;

                case "confirmed": // Confirmed successful payments
                    filter &= filterBuilder.Eq(t => t.PaymentStatus, "Paid");
                    break;

                case "recent": // Active unconfirmed temporary checkouts strictly within retention window
                    filter &= filterBuilder.Ne(t => t.PaymentStatus, "Paid") 
                           & filterBuilder.Ne(t => t.IsArchived, true)
                           & filterBuilder.Gte(t => t.CreatedDate, cutoffUtc);
                    break;

                case "archived": // Expired / Archived temporary checkouts
                    filter &= filterBuilder.Or(
                        filterBuilder.Eq(t => t.IsArchived, true),
                        filterBuilder.And(
                            filterBuilder.Ne(t => t.PaymentStatus, "Paid"),
                            filterBuilder.Lt(t => t.CreatedDate, cutoffUtc)
                        )
                    );
                    break;

                case "refunds": // Refunds and disputes
                    filter &= (filterBuilder.Eq(t => t.PaymentStatus, "RefundPending") | filterBuilder.Eq(t => t.PaymentStatus, "Refunded"));
                    break;

                case "all":
                default:
                    // By default, show confirmed payments and active unexpired checkouts (exclude expired/archived attempts)
                    filter &= filterBuilder.Or(
                        filterBuilder.Eq(t => t.PaymentStatus, "Paid"),
                        filterBuilder.And(
                            filterBuilder.Ne(t => t.PaymentStatus, "Paid"),
                            filterBuilder.Ne(t => t.IsArchived, true),
                            filterBuilder.Gte(t => t.CreatedDate, cutoffUtc)
                        )
                    );
                    break;
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                filter &= filterBuilder.Eq(t => t.PaymentStatus, status);
            }

            if (!string.IsNullOrWhiteSpace(activationStatus) && activationStatus != "All")
            {
                filter &= filterBuilder.Eq(t => t.ActivationStatus, activationStatus);
            }

            if (!string.IsNullOrWhiteSpace(billingCycle) && billingCycle != "All")
            {
                filter &= filterBuilder.Eq(t => t.BillingCycle, billingCycle);
            }

            if (!string.IsNullOrWhiteSpace(packageId) && packageId != "All")
            {
                filter &= filterBuilder.Eq(t => t.PackageId, packageId);
            }

            if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var parsedFrom))
            {
                filter &= filterBuilder.Gte(t => t.CreatedDate, parsedFrom.ToUniversalTime());
            }

            if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var parsedTo))
            {
                filter &= filterBuilder.Lte(t => t.CreatedDate, parsedTo.AddDays(1).ToUniversalTime());
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                filter &= (filterBuilder.Regex(t => t.TransactionRef, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.ReceiptNumber, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.ShopName, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.CustomerName, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.CustomerEmail, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.CustomerPhone, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.RazorpayPaymentId, new MongoDB.Bson.BsonRegularExpression(s, "i"))
                          | filterBuilder.Regex(t => t.RazorpayOrderId, new MongoDB.Bson.BsonRegularExpression(s, "i")));
            }

            int pageSize = 20;
            if (page < 1) page = 1;

            var totalCount = await _context.SaaSPaymentTransactions.CountDocumentsAsync(filter);
            var transactions = await _context.SaaSPaymentTransactions
                .Find(filter)
                .SortByDescending(t => t.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var packages = await _context.SubscriptionPackages.Find(_ => true).ToListAsync();

            ViewBag.Metrics = metrics;
            ViewBag.PlatformSettings = platformSettings;
            ViewBag.ActiveTab = activeTab;
            ViewBag.Packages = packages;
            ViewBag.CurrentStatus = status ?? "All";
            ViewBag.CurrentActivationStatus = activationStatus ?? "All";
            ViewBag.CurrentBillingCycle = billingCycle ?? "All";
            ViewBag.CurrentPackageId = packageId ?? "All";
            ViewBag.CurrentSearch = search ?? string.Empty;
            ViewBag.FromDate = fromDate ?? string.Empty;
            ViewBag.ToDate = toDate ?? string.Empty;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View(transactions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryActivation(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ToastMessage"] = "Invalid transaction ID.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Revenue));
            }

            var (success, errorMsg) = await _saasPaymentService.ActivateSubscriptionFromPaymentAsync(id);
            if (success)
            {
                TempData["ToastMessage"] = "Subscription activated successfully from verified payment record.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = $"Activation retry failed: {errorMsg}";
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(Revenue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReconcileTransaction(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ToastMessage"] = "Invalid transaction ID.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Revenue));
            }

            var (success, message, txn) = await _saasPaymentService.ReconcileSingleTransactionAsync(id);
            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "info";

            return RedirectToAction(nameof(Revenue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunBatchReconciliation()
        {
            var (reconciled, activated, failed) = await _saasPaymentService.ReconcilePendingPaymentsAsync();
            TempData["ToastMessage"] = $"Batch reconciliation finished: {reconciled} checked, {activated} activated, {failed} pending/failed.";
            TempData["ToastType"] = "info";
            return RedirectToAction(nameof(Revenue));
        }

        [HttpGet]
        public async Task<IActionResult> DownloadReceipt(string id)
        {
            var txn = await _context.SaaSPaymentTransactions.Find(t => t.Id == id).FirstOrDefaultAsync();
            if (txn == null)
            {
                return NotFound("Payment transaction record not found.");
            }

            byte[]? pdfBytes = txn.ReceiptPdfBytes;
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                var package = !string.IsNullOrEmpty(txn.PackageId) 
                    ? await _context.SubscriptionPackages.Find(p => p.Id == txn.PackageId).FirstOrDefaultAsync() 
                    : null;
                var settings = await _platformSettingsService.GetSettingsAsync();

                pdfBytes = _receiptService.GeneratePaymentReceiptPdf(txn, package, settings);
                txn.ReceiptPdfBytes = pdfBytes;
            }

            var fileName = $"Receipt-{txn.ReceiptNumber ?? txn.TransactionRef}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryReceiptEmail(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ToastMessage"] = "Invalid transaction ID.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Revenue));
            }

            var (success, errorMsg) = await _saasPaymentService.RetryReceiptEmailAsync(id);
            if (success)
            {
                TempData["ToastMessage"] = "Payment receipt and GST invoice generated & emailed successfully.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = $"Failed to email receipt: {errorMsg}";
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(Revenue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePaymentRecoverySettings(
            int retentionValue,
            string retentionUnit,
            int pendingVerificationHours,
            bool enableFollowUp = true,
            bool archiveBeforeCleanup = true,
            bool autoRestoreLate = true,
            int messageDuration = 60)
        {
            try
            {
                var settings = await _platformSettingsService.GetSettingsAsync();
                settings.TemporaryCheckoutRetentionValue = retentionValue > 0 ? retentionValue : 3;
                settings.TemporaryCheckoutRetentionUnit = !string.IsNullOrWhiteSpace(retentionUnit) ? retentionUnit.Trim() : "Hours";
                settings.PendingVerificationRetentionHours = pendingVerificationHours > 0 ? pendingVerificationHours : 24;
                settings.EnableCustomerFollowUpMessages = enableFollowUp;
                settings.ArchiveBeforeCleanup = archiveBeforeCleanup;
                settings.AutoRestoreLatePayments = autoRestoreLate;
                settings.CustomerReassuranceMessageDurationMinutes = messageDuration > 0 ? messageDuration : 60;
                settings.LastUpdated = DateTime.UtcNow;
                settings.UpdatedBy = User.Identity?.Name ?? "SuperAdmin";

                await _platformSettingsService.SaveSettingsAsync(settings, settings.UpdatedBy);

                TempData["ToastMessage"] = "Payment recovery and temporary retention settings saved successfully.";
                TempData["ToastType"] = "success";
            }
            catch (Exception ex)
            {
                TempData["ToastMessage"] = $"Failed to save recovery settings: {ex.Message}";
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(Revenue), new { tab = "settings" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunRetentionCleanup()
        {
            try
            {
                int archivedCount = await _saasPaymentService.CleanupExpiredTemporaryCheckoutsAsync();
                TempData["ToastMessage"] = $"Retention policy cleanup completed. {archivedCount} expired temporary checkout attempt(s) archived safely.";
                TempData["ToastType"] = "info";
            }
            catch (Exception ex)
            {
                TempData["ToastMessage"] = $"Error executing retention cleanup: {ex.Message}";
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction(nameof(Revenue), new { tab = "settings" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveRecoveryCase(string id, string notes)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ToastMessage"] = "Transaction ID is required.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Revenue), new { tab = "recovery" });
            }

            var adminUser = User.Identity?.Name ?? "SuperAdmin";
            var (success, msg) = await _saasPaymentService.ResolvePaymentCaseManuallyAsync(id, notes, adminUser);

            TempData["ToastMessage"] = success ? msg : $"Could not resolve recovery case: {msg}";
            TempData["ToastType"] = success ? "success" : "danger";

            return RedirectToAction(nameof(Revenue), new { tab = "recovery" });
        }

        #endregion
    }

    public class MarkdownPreviewDto
    {
        public string Markdown { get; set; } = string.Empty;
    }
}
