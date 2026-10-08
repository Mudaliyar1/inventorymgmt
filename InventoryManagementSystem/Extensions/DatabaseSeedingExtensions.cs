using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Extensions
{
    public static class DatabaseSeedingExtensions
    {
        public static async Task SeedDatabaseAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

            // 1. Seed or Resolve Primary Tenant
            var primaryTenant = await context.Tenants.Find(t => t.TenantCode == "SHOP-PRIMARY").FirstOrDefaultAsync();
            if (primaryTenant == null)
            {
                primaryTenant = new Tenant
                {
                    TenantCode = "SHOP-PRIMARY",
                    ShopName = "Primary Mobile Shop",
                    OwnerName = "Primary Admin",
                    ContactEmail = "admin@sims.com",
                    Phone = "1234567890",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await context.Tenants.InsertOneAsync(primaryTenant);
                Console.WriteLine($"[TENANT MIGRATION] Created primary tenant with generated Id: {primaryTenant.Id}");
            }

            var primaryTenantId = primaryTenant.Id;

            // Seed Roles
            var superAdminRole = await context.Roles.Find(r => r.Name == Role.SuperAdmin).FirstOrDefaultAsync();
            if (superAdminRole == null)
            {
                await context.Roles.InsertOneAsync(new Role { Name = Role.SuperAdmin, Description = "SaaS Platform Super Administrator" });
            }

            var rolesCount = await context.Roles.CountDocumentsAsync(_ => true);
            if (rolesCount == 0)
            {
                await context.Roles.InsertManyAsync(new[]
                {
                    new Role { Name = Role.Admin, Description = "Administrator with full system privileges" },
                    new Role { Name = Role.Staff, Description = "Staff member with inventory operation privileges" },
                    new Role { Name = Role.SuperAdmin, Description = "SaaS Platform Super Administrator" }
                });
            }

            // Seed SuperAdmin Platform User
            var superAdminUser = await context.Users.Find(u => u.Username == "superadmin" || u.Role == Role.SuperAdmin).FirstOrDefaultAsync();
            if (superAdminUser == null)
            {
                superAdminUser = new User
                {
                    TenantId = null,
                    EmployeeId = null,
                    Username = "superadmin",
                    Email = "superadmin@sims.com",
                    FullName = "SaaS Platform SuperAdmin",
                    PhoneNumber = "0000000000",
                    Role = Role.SuperAdmin,
                    IsLocked = false,
                    ProfilePictureUrl = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&q=80&w=200",
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin123!")
                };
                await context.Users.InsertOneAsync(superAdminUser);
                Console.WriteLine("[SEEDING] Created Platform SuperAdmin user: superadmin / SuperAdmin123!");
            }
            else
            {
                superAdminUser.TenantId = null;
                superAdminUser.EmployeeId = null;
                superAdminUser.Role = Role.SuperAdmin;
                await context.Users.ReplaceOneAsync(u => u.Id == superAdminUser.Id, superAdminUser);
            }

            // Seed Primary Shop Admin User
            var adminUser = await context.Users.Find(u => u.Username == "admin").FirstOrDefaultAsync();
            if (adminUser == null)
            {
                adminUser = new User
                {
                    TenantId = primaryTenantId,
                    EmployeeId = "EMP-1000",
                    Username = "admin",
                    Email = "admin@sims.com",
                    FullName = "System Administrator",
                    PhoneNumber = "1234567890",
                    Role = Role.Admin,
                    IsLocked = false,
                    ProfilePictureUrl = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&q=80&w=200",
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPassword123")
                };
                await context.Users.InsertOneAsync(adminUser);
                Console.WriteLine("[SEEDING] Created Primary Shop Admin user: admin / AdminPassword123");
            }

            // Seed Initial Settings
            var settingsCount = await context.Settings.CountDocumentsAsync(_ => true);
            if (settingsCount == 0)
            {
                var settings = new Settings
                {
                    TenantId = primaryTenantId,
                    CompanyName = "Smart Inventory Management System (SIMS)",
                    Currency = "INR",
                    GstPercentage = 18.0,
                    Theme = "dark",
                    UpdatedBy = "System",
                    LastUpdated = DateTime.UtcNow
                };
                await context.Settings.InsertOneAsync(settings);
            }

            // Seed Default Subscription Packages
            var pkgCount = await context.SubscriptionPackages.CountDocumentsAsync(_ => true);
            if (pkgCount == 0)
            {
                await context.SubscriptionPackages.InsertManyAsync(new[]
                {
                    new SubscriptionPackage
                    {
                        Name = "Basic Plan",
                        Description = "Essential tools for small mobile shops.",
                        MonthlyPrice = 999,
                        YearlyPrice = 9990,
                        MaxEmployees = 3,
                        MaxProducts = 500,
                        MaxSuppliers = 20,
                        EnabledModules = new System.Collections.Generic.List<string> { "Products", "Inventory", "POS", "Customers" },
                        DisplayOrder = 1
                    },
                    new SubscriptionPackage
                    {
                        Name = "Professional Plan",
                        Description = "Advanced features including suppliers, repairs, and returns.",
                        MonthlyPrice = 1999,
                        YearlyPrice = 19990,
                        MaxEmployees = 10,
                        MaxProducts = 5000,
                        MaxSuppliers = 100,
                        EnabledModules = new System.Collections.Generic.List<string> { "Products", "Inventory", "POS", "Customers", "Suppliers", "Returns", "Repairs", "Reports" },
                        DisplayOrder = 2
                    },
                    new SubscriptionPackage
                    {
                        Name = "Enterprise Plan",
                        Description = "Full suite with unlimited scale, trade-in, and priority support.",
                        MonthlyPrice = 3999,
                        YearlyPrice = 39990,
                        MaxEmployees = 100,
                        MaxProducts = 100000,
                        MaxSuppliers = 1000,
                        EnabledModules = new System.Collections.Generic.List<string> { "Products", "Inventory", "POS", "Customers", "Suppliers", "Returns", "Repairs", "TradeIn", "Reports", "AdvancedAnalytics" },
                        DisplayOrder = 3
                    }
                });
            }

            // Cleanup any orphaned supplier products/categories from previously deleted suppliers
            try
            {
                var supplierService = scope.ServiceProvider.GetRequiredService<InventoryManagementSystem.Interfaces.ISupplierService>();
                await supplierService.CleanupOrphanedSupplierDataAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEEDING NOTICE] Orphaned supplier cleanup notice: {ex.Message}");
            }
        }
    }
}
