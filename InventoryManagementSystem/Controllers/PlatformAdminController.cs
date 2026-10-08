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

namespace InventoryManagementSystem.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Route("platform/[action]")]
    public class PlatformAdminController : Controller
    {
        private readonly MongoDbContext _context;
        private readonly ILicenseService _licenseService;
        private readonly IAuthService _authService;

        public PlatformAdminController(MongoDbContext context, ILicenseService licenseService, IAuthService authService)
        {
            _context = context;
            _licenseService = licenseService;
            _authService = authService;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var tenants = await _context.Tenants.Find(_ => true).ToListAsync();
            var licenses = await _context.TenantLicenses.Find(_ => true).ToListAsync();
            var packages = await _context.SubscriptionPackages.Find(_ => true).ToListAsync();
            var payments = await _context.SaaSPaymentTransactions.Find(_ => true).ToListAsync();

            ViewBag.TotalTenants = tenants.Count;
            ViewBag.ActiveTenants = tenants.Count(t => t.Status == "Active");
            ViewBag.TrialTenants = tenants.Count(t => t.Status == "Trial" || licenses.Any(l => l.TenantId == t.Id && l.Status == "Trial"));
            ViewBag.ExpiredTenants = licenses.Count(l => l.ExpiryDate < DateTime.UtcNow);

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
            var package = await _context.SubscriptionPackages.Find(p => p.Id == tenant.PackageId).FirstOrDefaultAsync();
            var allPackages = await _context.SubscriptionPackages.Find(_ => true).ToListAsync();

            var suppliers = await _context.Suppliers.Find(s => s.TenantId == id).ToListAsync();
            var users = await _context.Users.Find(u => u.TenantId == id && u.Role != Role.SuperAdmin).ToListAsync();
            var products = await _context.Products.Find(p => p.TenantId == id).ToListAsync();
            var customers = await _context.Customers.Find(c => c.TenantId == id).ToListAsync();
            var sales = await _context.Sales.Find(s => s.TenantId == id).ToListAsync();

            ViewBag.License = license;
            ViewBag.Package = package;
            ViewBag.Packages = allPackages;
            ViewBag.Suppliers = suppliers;
            ViewBag.Users = users;
            ViewBag.StaffCount = users.Count(u => u.Role != Role.Admin);
            ViewBag.ProductsCount = products.Count;
            ViewBag.CustomersCount = customers.Count;
            ViewBag.SalesCount = sales.Count;

            return View(tenant);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTenant()
        {
            ViewBag.Packages = await _licenseService.GetAllPackagesAsync();
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
                ViewBag.Packages = await _licenseService.GetAllPackagesAsync();
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
            var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
            if (tenant != null)
            {
                tenant.Status = status;
                tenant.UpdatedAt = DateTime.UtcNow;
                await _context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);

                TempData["ToastMessage"] = $"Shop '{tenant.ShopName}' status updated to {status}.";
                TempData["ToastType"] = "info";
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(TenantDetails), new { id = tenantId });
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

        [HttpGet]
        public async Task<IActionResult> Packages()
        {
            var packages = await _licenseService.GetAllPackagesAsync();
            return View(packages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePackage(SubscriptionPackage package, string enabledModulesCsv)
        {
            if (!string.IsNullOrWhiteSpace(enabledModulesCsv))
            {
                package.EnabledModules = enabledModulesCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }

            await _licenseService.CreateOrUpdatePackageAsync(package);
            TempData["ToastMessage"] = $"Subscription Package '{package.Name}' saved successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Packages));
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
    }
}
