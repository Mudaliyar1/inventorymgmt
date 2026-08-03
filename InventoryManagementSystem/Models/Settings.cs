using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryManagementSystem.Models
{
    public class Settings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("CompanyName")]
        public string CompanyName { get; set; } = "Smart Inventory Management System";

        [BsonElement("CompanyLogoUrl")]
        public string CompanyLogoUrl { get; set; } = string.Empty;

        [BsonElement("Currency")]
        public string Currency { get; set; } = "INR";

        [BsonElement("GstPercentage")]
        public double GstPercentage { get; set; } = 18.0;

        [BsonElement("Theme")]
        public string Theme { get; set; } = "dark"; // dark, light, glass

        [BsonElement("UpdatedBy")]
        public string UpdatedBy { get; set; } = "System";

        [BsonElement("LastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [BsonElement("CompanyEmail")]
        public string CompanyEmail { get; set; } = "support@sims.com";

        [BsonElement("CompanyPhone")]
        public string CompanyPhone { get; set; } = "+91 98765 43210";

        [BsonElement("Address")]
        public string Address { get; set; } = "123 Business Hub, Mumbai, India";

        [BsonElement("CurrencySymbol")]
        public string CurrencySymbol { get; set; } = "₹";

        [BsonElement("GstRate")]
        public decimal GstRate { get; set; } = 18.0m;

        [BsonElement("LowStockThreshold")]
        public int LowStockThreshold { get; set; } = 5;
    }
}
