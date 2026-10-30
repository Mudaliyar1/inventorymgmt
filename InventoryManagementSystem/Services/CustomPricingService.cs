using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class CustomPricingService : ICustomPricingService
    {
        private readonly MongoDbContext _context;

        public CustomPricingService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<CustomPricingConfiguration> GetConfigurationAsync()
        {
            var config = await _context.CustomPricingConfigurations.Find(_ => true).FirstOrDefaultAsync();
            if (config == null)
            {
                config = new CustomPricingConfiguration
                {
                    BaseMonthlyPrice = 999m,
                    AdminUnitPrice = 150m,
                    EmployeeUnitPrice = 75m,
                    ProductPricingModel = "Tiered",
                    ProductUnitPrice = 0.05m,
                    ProductTiers = new List<ProductTierPricing>
                    {
                        new ProductTierPricing { TierName = "Starter (Up to 1,000 products)", MinProducts = 0, MaxProducts = 1000, FlatPrice = 0m, PerUnitRate = 0m },
                        new ProductTierPricing { TierName = "Growth (Up to 10,000 products)", MinProducts = 1001, MaxProducts = 10000, FlatPrice = 499m, PerUnitRate = 0m },
                        new ProductTierPricing { TierName = "Mid-Market (Up to 100,000 products)", MinProducts = 10001, MaxProducts = 100000, FlatPrice = 1499m, PerUnitRate = 0m },
                        new ProductTierPricing { TierName = "Enterprise High-Volume (Up to 1,000,000 products)", MinProducts = 100001, MaxProducts = 1000000, FlatPrice = 3999m, PerUnitRate = 0m },
                        new ProductTierPricing { TierName = "Ultra-Scale (Above 1,000,000 products)", MinProducts = 1000001, MaxProducts = 10000000, FlatPrice = 7999m, PerUnitRate = 0m }
                    },
                    SupplierUnitPrice = 5m,
                    DeviceUnitPrice = 0m,
                    FeatureRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
                    AnnualDiscountPercent = 20m,
                    MinAdmins = 1,
                    MaxAdmins = 100,
                    MinEmployees = 1,
                    MaxEmployees = 1000,
                    MinProducts = 100,
                    MaxProducts = 5000000,
                    MinSuppliers = 1,
                    MaxSuppliers = 10000,
                    MinDevices = 0,
                    MaxDevices = 1000000,
                    IsEnabled = true,
                    AllowCustomQuotes = true,
                    UpdatedBy = "System",
                    LastUpdated = DateTime.UtcNow
                };

                await _context.CustomPricingConfigurations.InsertOneAsync(config);
            }

            return config;
        }

        public async Task SaveConfigurationAsync(CustomPricingConfiguration config, string updatedBy)
        {
            config.LastUpdated = DateTime.UtcNow;
            config.UpdatedBy = updatedBy;

            var existing = await _context.CustomPricingConfigurations.Find(_ => true).FirstOrDefaultAsync();
            if (existing == null)
            {
                await _context.CustomPricingConfigurations.InsertOneAsync(config);
            }
            else
            {
                config.Id = existing.Id;
                await _context.CustomPricingConfigurations.ReplaceOneAsync(c => c.Id == existing.Id, config);
            }
        }

        public (decimal MonthlyPrice, decimal YearlyPrice, string BreakdownJson) CalculatePrice(
            CustomPricingConfiguration config,
            int admins,
            int employees,
            int products,
            int suppliers,
            int devices,
            List<string>? selectedFeatureKeys)
        {
            if (config == null) return (0m, 0m, "{}");

            // Clamp quantities to valid non-negative ranges
            admins = Math.Max(0, admins);
            employees = Math.Max(0, employees);
            products = Math.Max(0, products);
            suppliers = Math.Max(0, suppliers);
            devices = Math.Max(0, devices);

            decimal basePrice = config.BaseMonthlyPrice;
            decimal adminCost = admins * config.AdminUnitPrice;
            decimal employeeCost = employees * config.EmployeeUnitPrice;

            // Product pricing
            decimal productCost = 0m;
            string appliedTierName = "N/A";
            if (string.Equals(config.ProductPricingModel, "Tiered", StringComparison.OrdinalIgnoreCase) && config.ProductTiers != null && config.ProductTiers.Any())
            {
                var sortedTiers = config.ProductTiers.OrderBy(t => t.MaxProducts).ToList();
                var matchedTier = sortedTiers.FirstOrDefault(t => products <= t.MaxProducts) ?? sortedTiers.Last();
                appliedTierName = matchedTier.TierName;
                productCost = matchedTier.FlatPrice + (products * matchedTier.PerUnitRate);
            }
            else
            {
                productCost = products * config.ProductUnitPrice;
                appliedTierName = "Per Unit Model";
            }

            decimal supplierCost = suppliers * config.SupplierUnitPrice;

            // Feature addons
            decimal featuresCost = 0m;
            var featureCostMap = new Dictionary<string, decimal>();
            if (selectedFeatureKeys != null && config.FeatureRates != null)
            {
                foreach (var fKey in selectedFeatureKeys)
                {
                    if (config.FeatureRates.TryGetValue(fKey, out var rate) && rate > 0)
                    {
                        featuresCost += rate;
                        featureCostMap[fKey] = rate;
                    }
                }
            }

            decimal monthlyTotal = basePrice + adminCost + employeeCost + productCost + supplierCost + featuresCost;
            if (monthlyTotal < 0m) monthlyTotal = 0m;

            // Yearly pricing calculation
            decimal discountMultiplier = 1m - (Math.Clamp(config.AnnualDiscountPercent, 0m, 100m) / 100m);
            decimal yearlyTotal = monthlyTotal * 12m * discountMultiplier;
            if (yearlyTotal < 0m) yearlyTotal = 0m;

            var breakdown = new
            {
                BaseMonthlyPrice = basePrice,
                Admins = new { Count = admins, Rate = config.AdminUnitPrice, Cost = adminCost },
                Employees = new { Count = employees, Rate = config.EmployeeUnitPrice, Cost = employeeCost },
                Products = new { Count = products, Model = config.ProductPricingModel, AppliedTier = appliedTierName, Cost = productCost },
                Suppliers = new { Count = suppliers, Rate = config.SupplierUnitPrice, Cost = supplierCost },
                FeaturesCost = featuresCost,
                FeatureCostDetails = featureCostMap,
                AnnualDiscountPercent = config.AnnualDiscountPercent,
                CalculatedMonthly = Math.Round(monthlyTotal, 2),
                CalculatedYearly = Math.Round(yearlyTotal, 2),
                CalculatedAt = DateTime.UtcNow
            };

            return (Math.Round(monthlyTotal, 2), Math.Round(yearlyTotal, 2), JsonSerializer.Serialize(breakdown));
        }

        public async Task<CustomPackageInquiry> CreateInquiryAsync(CustomPackageInquiry inquiry)
        {
            var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var count = await _context.CustomPackageInquiries.CountDocumentsAsync(_ => true);
            inquiry.InquiryNumber = $"INQ-CP-{todayStr}-{(count + 1):D4}";
            inquiry.CreatedAt = DateTime.UtcNow;
            inquiry.UpdatedAt = DateTime.UtcNow;
            inquiry.Status = "New";

            if (inquiry.FollowUpHistory == null)
            {
                inquiry.FollowUpHistory = new List<InquiryFollowUp>();
            }

            inquiry.FollowUpHistory.Add(new InquiryFollowUp
            {
                Note = "Inquiry received from public package calculator.",
                CreatedBy = "System",
                Timestamp = DateTime.UtcNow,
                StatusChangedTo = "New"
            });

            await _context.CustomPackageInquiries.InsertOneAsync(inquiry);
            return inquiry;
        }

        public async Task<List<CustomPackageInquiry>> GetInquiriesAsync(string? search = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var filter = Builders<CustomPackageInquiry>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filter &= Builders<CustomPackageInquiry>.Filter.Eq(i => i.Status, status);
            }

            if (fromDate.HasValue)
            {
                filter &= Builders<CustomPackageInquiry>.Filter.Gte(i => i.CreatedAt, fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                filter &= Builders<CustomPackageInquiry>.Filter.Lte(i => i.CreatedAt, endOfDay);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchClean = search.Trim();
                var sFilter = Builders<CustomPackageInquiry>.Filter.Or(
                    Builders<CustomPackageInquiry>.Filter.Regex(i => i.InquiryNumber, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<CustomPackageInquiry>.Filter.Regex(i => i.ContactName, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<CustomPackageInquiry>.Filter.Regex(i => i.BusinessName, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<CustomPackageInquiry>.Filter.Regex(i => i.Email, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<CustomPackageInquiry>.Filter.Regex(i => i.Phone, new MongoDB.Bson.BsonRegularExpression(searchClean, "i"))
                );
                filter &= sFilter;
            }

            return await _context.CustomPackageInquiries.Find(filter)
                .SortByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<CustomPackageInquiry?> GetInquiryByIdAsync(string id)
        {
            return await _context.CustomPackageInquiries.Find(i => i.Id == id).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateInquiryStatusAsync(string id, string status, string? note, string updatedBy, string? convertedTenantId = null)
        {
            var inquiry = await GetInquiryByIdAsync(id);
            if (inquiry == null) return false;

            inquiry.Status = status;
            inquiry.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(convertedTenantId))
            {
                inquiry.ConvertedTenantId = convertedTenantId;
            }

            if (!string.IsNullOrWhiteSpace(note))
            {
                inquiry.AdminNotes = string.IsNullOrWhiteSpace(inquiry.AdminNotes) ? note : $"{inquiry.AdminNotes}\n[{DateTime.UtcNow:g}] {note}";
            }

            if (inquiry.FollowUpHistory == null) inquiry.FollowUpHistory = new List<InquiryFollowUp>();
            inquiry.FollowUpHistory.Add(new InquiryFollowUp
            {
                Note = note ?? $"Status updated to {status}",
                CreatedBy = updatedBy,
                Timestamp = DateTime.UtcNow,
                StatusChangedTo = status
            });

            var result = await _context.CustomPackageInquiries.ReplaceOneAsync(i => i.Id == id, inquiry);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateConfirmationEmailStatusAsync(string id, string status, string? error)
        {
            var inquiry = await GetInquiryByIdAsync(id);
            if (inquiry == null) return false;

            inquiry.ConfirmationEmailStatus = status;
            if (status == "Sent")
            {
                inquiry.ConfirmationEmailSentAt = DateTime.UtcNow;
                inquiry.ConfirmationEmailError = null;
            }
            else
            {
                inquiry.ConfirmationEmailError = error;
            }
            inquiry.UpdatedAt = DateTime.UtcNow;

            if (inquiry.FollowUpHistory == null) inquiry.FollowUpHistory = new List<InquiryFollowUp>();
            inquiry.FollowUpHistory.Add(new InquiryFollowUp
            {
                Note = status == "Sent" 
                    ? "Confirmation email successfully delivered to customer." 
                    : $"Customer confirmation email failed: {error}",
                CreatedBy = "System",
                Timestamp = DateTime.UtcNow
            });

            var result = await _context.CustomPackageInquiries.ReplaceOneAsync(i => i.Id == id, inquiry);
            return result.ModifiedCount > 0;
        }

        public async Task<(bool Success, string Message)> DeleteInquiryAsync(string id, string deletedBy)
        {
            var inquiry = await GetInquiryByIdAsync(id);
            if (inquiry == null)
            {
                return (false, "Custom package inquiry was not found.");
            }

            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Closed", "Converted", "Completed" };
            if (!allowedStatuses.Contains(inquiry.Status))
            {
                return (false, $"Inquiry #{inquiry.InquiryNumber} cannot be deleted because it is currently '{inquiry.Status}'. Only closed or completed inquiries can be deleted.");
            }

            var result = await _context.CustomPackageInquiries.DeleteOneAsync(i => i.Id == id);
            if (result.DeletedCount > 0)
            {
                return (true, $"Custom quote inquiry #{inquiry.InquiryNumber} ({inquiry.ContactName}) has been deleted successfully.");
            }

            return (false, "Failed to delete custom package inquiry.");
        }

        public async Task<(int DeletedCount, string Message)> BulkDeleteInquiriesAsync(List<string> ids, string deletedBy)
        {
            if (ids == null || ids.Count == 0)
            {
                return (0, "No custom package inquiries selected for deletion.");
            }

            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Closed", "Converted", "Completed" };
            var inquiries = await _context.CustomPackageInquiries.Find(i => ids.Contains(i.Id)).ToListAsync();
            var deletableIds = inquiries.Where(i => allowedStatuses.Contains(i.Status)).Select(i => i.Id).ToList();
            var skippedCount = inquiries.Count - deletableIds.Count;

            if (deletableIds.Count == 0)
            {
                return (0, "None of the selected inquiries could be deleted. Active/pending inquiries are protected. Only closed or completed inquiries can be deleted.");
            }

            var result = await _context.CustomPackageInquiries.DeleteManyAsync(i => deletableIds.Contains(i.Id));
            int deleted = (int)result.DeletedCount;

            string msg = $"{deleted} closed/completed inquiry record(s) deleted permanently.";
            if (skippedCount > 0)
            {
                msg += $" {skippedCount} active/pending inquiry record(s) were protected and skipped.";
            }

            return (deleted, msg);
        }
    }
}
