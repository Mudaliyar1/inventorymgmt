using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class LicenseService : ILicenseService
    {
        private readonly MongoDbContext _context;

        public LicenseService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<TenantLicense?> GetActiveLicenseAsync(string tenantId)
        {
            if (string.IsNullOrEmpty(tenantId)) return null;
            return await _context.TenantLicenses
                .Find(l => l.TenantId == tenantId)
                .SortByDescending(l => l.ExpiryDate)
                .FirstOrDefaultAsync();
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
            var license = new TenantLicense
            {
                TenantId = tenantId,
                PackageId = packageId,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(days),
                Status = "Trial",
                AutoRenew = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.TenantLicenses.InsertOneAsync(license);
            return license;
        }

        public async Task<bool> RenewOrExtendLicenseAsync(string tenantId, string packageId, int durationDays)
        {
            var existing = await GetActiveLicenseAsync(tenantId);
            if (existing != null)
            {
                var newStart = existing.ExpiryDate > DateTime.UtcNow ? existing.ExpiryDate : DateTime.UtcNow;
                existing.PackageId = packageId;
                existing.ExpiryDate = newStart.AddDays(durationDays);
                existing.Status = "Active";
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.TenantLicenses.ReplaceOneAsync(l => l.Id == existing.Id, existing);

                // Also update Tenant PackageId
                var tenant = await _context.Tenants.Find(t => t.Id == tenantId).FirstOrDefaultAsync();
                if (tenant != null)
                {
                    tenant.PackageId = packageId;
                    tenant.Status = "Active";
                    tenant.UpdatedAt = DateTime.UtcNow;
                    await _context.Tenants.ReplaceOneAsync(t => t.Id == tenant.Id, tenant);
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
                return true;
            }
        }

        public async Task<IEnumerable<SubscriptionPackage>> GetAllPackagesAsync()
        {
            return await _context.SubscriptionPackages.Find(_ => true).SortBy(p => p.DisplayOrder).ToListAsync();
        }

        public async Task<IEnumerable<SubscriptionPackage>> GetPublicPackagesAsync()
        {
            return await _context.SubscriptionPackages.Find(p => p.IsActive && !p.IsCustom).SortBy(p => p.DisplayOrder).ToListAsync();
        }

        public async Task<IEnumerable<SubscriptionPackage>> GetCustomPackagesAsync()
        {
            return await _context.SubscriptionPackages.Find(p => p.IsCustom).SortByDescending(p => p.CreatedAt).ToListAsync();
        }

        public async Task<SubscriptionPackage?> GetPackageByIdAsync(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return null;
            return await _context.SubscriptionPackages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
        }

        public async Task CreateOrUpdatePackageAsync(SubscriptionPackage package)
        {
            if (string.IsNullOrEmpty(package.Id))
            {
                package.CreatedAt = DateTime.UtcNow;
                await _context.SubscriptionPackages.InsertOneAsync(package);
            }
            else
            {
                await _context.SubscriptionPackages.ReplaceOneAsync(p => p.Id == package.Id, package);
            }
        }
    }
}
