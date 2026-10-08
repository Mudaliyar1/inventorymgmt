using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class SubscriptionPackage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty; // Basic, Professional, Enterprise

        [BsonElement("Description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("MonthlyPrice")]
        public decimal MonthlyPrice { get; set; }

        [BsonElement("YearlyPrice")]
        public decimal YearlyPrice { get; set; }

        [BsonElement("TrialDays")]
        public int TrialDays { get; set; } = 14;

        [BsonElement("BillingCycle")]
        public string BillingCycle { get; set; } = "Monthly"; // Monthly, Yearly, Both

        [BsonElement("MaxEmployees")]
        public int MaxEmployees { get; set; } = 5;

        [BsonElement("MaxAdmins")]
        public int MaxAdmins { get; set; } = 2;

        [BsonElement("MaxProducts")]
        public int MaxProducts { get; set; } = 1000;

        [BsonElement("MaxSuppliers")]
        public int MaxSuppliers { get; set; } = 50;

        [BsonElement("EnabledModules")]
        public List<string> EnabledModules { get; set; } = new List<string>();

        [BsonElement("EnabledFeatures")]
        public List<string> EnabledFeatures { get; set; } = new List<string>(); // Selected FeatureKeys (e.g., POS_BILLING, SUPPLIERS)

        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("IsFeatured")]
        public bool IsFeatured { get; set; } = false;

        [BsonElement("DisplayOrder")]
        public int DisplayOrder { get; set; } = 1;

        [BsonElement("IsCustom")]
        public bool IsCustom { get; set; } = false; // Hidden from public pricing page when true

        [BsonElement("AssignedTenantId")]
        public string? AssignedTenantId { get; set; } // Optional: Specific shop ID this package is tailored for

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
