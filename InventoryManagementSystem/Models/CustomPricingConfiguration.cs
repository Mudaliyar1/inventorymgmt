using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class CustomPricingConfiguration
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("BaseMonthlyPrice")]
        public decimal BaseMonthlyPrice { get; set; } = 999m;

        [BsonElement("AdminUnitPrice")]
        public decimal AdminUnitPrice { get; set; } = 150m;

        [BsonElement("EmployeeUnitPrice")]
        public decimal EmployeeUnitPrice { get; set; } = 75m;

        // Pricing model: "Tiered" or "PerUnit"
        [BsonElement("ProductPricingModel")]
        public string ProductPricingModel { get; set; } = "Tiered";

        [BsonElement("ProductUnitPrice")]
        public decimal ProductUnitPrice { get; set; } = 0.05m;

        [BsonElement("ProductTiers")]
        public List<ProductTierPricing> ProductTiers { get; set; } = new List<ProductTierPricing>
        {
            new ProductTierPricing { TierName = "Starter (Up to 1,000 products)", MinProducts = 0, MaxProducts = 1000, FlatPrice = 0m, PerUnitRate = 0m },
            new ProductTierPricing { TierName = "Growth (Up to 10,000 products)", MinProducts = 1001, MaxProducts = 10000, FlatPrice = 499m, PerUnitRate = 0m },
            new ProductTierPricing { TierName = "Mid-Market (Up to 100,000 products)", MinProducts = 10001, MaxProducts = 100000, FlatPrice = 1499m, PerUnitRate = 0m },
            new ProductTierPricing { TierName = "Enterprise High-Volume (Up to 1,000,000 products)", MinProducts = 100001, MaxProducts = 1000000, FlatPrice = 3999m, PerUnitRate = 0m },
            new ProductTierPricing { TierName = "Ultra-Scale (Above 1,000,000 products)", MinProducts = 1000001, MaxProducts = 10000000, FlatPrice = 7999m, PerUnitRate = 0m }
        };

        [BsonElement("SupplierUnitPrice")]
        public decimal SupplierUnitPrice { get; set; } = 5m;

        [BsonElement("DeviceUnitPrice")]
        public decimal DeviceUnitPrice { get; set; } = 2m;

        // Optional per-feature monthly fee (FeatureKey -> fee). Unlisted keys are ₹0.
        [BsonElement("FeatureRates")]
        public Dictionary<string, decimal> FeatureRates { get; set; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        [BsonElement("AnnualDiscountPercent")]
        public decimal AnnualDiscountPercent { get; set; } = 20m;

        // Min & Max validation boundaries
        [BsonElement("MinAdmins")]
        public int MinAdmins { get; set; } = 1;

        [BsonElement("MaxAdmins")]
        public int MaxAdmins { get; set; } = 100;

        [BsonElement("MinEmployees")]
        public int MinEmployees { get; set; } = 1;

        [BsonElement("MaxEmployees")]
        public int MaxEmployees { get; set; } = 1000;

        [BsonElement("MinProducts")]
        public int MinProducts { get; set; } = 100;

        [BsonElement("MaxProducts")]
        public int MaxProducts { get; set; } = 5000000;

        [BsonElement("MinSuppliers")]
        public int MinSuppliers { get; set; } = 1;

        [BsonElement("MaxSuppliers")]
        public int MaxSuppliers { get; set; } = 10000;

        [BsonElement("MinDevices")]
        public int MinDevices { get; set; } = 0;

        [BsonElement("MaxDevices")]
        public int MaxDevices { get; set; } = 1000000;

        [BsonElement("IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        [BsonElement("AllowCustomQuotes")]
        public bool AllowCustomQuotes { get; set; } = true;

        [BsonElement("LastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedBy")]
        public string UpdatedBy { get; set; } = "System";
    }

    [BsonIgnoreExtraElements]
    public class ProductTierPricing
    {
        [BsonElement("TierName")]
        public string TierName { get; set; } = string.Empty;

        [BsonElement("MinProducts")]
        public int MinProducts { get; set; }

        [BsonElement("MaxProducts")]
        public int MaxProducts { get; set; }

        [BsonElement("FlatPrice")]
        public decimal FlatPrice { get; set; }

        [BsonElement("PerUnitRate")]
        public decimal PerUnitRate { get; set; }
    }
}
