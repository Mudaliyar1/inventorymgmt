using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class Tenant
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("TenantCode")]
        public string TenantCode { get; set; } = string.Empty; // e.g. SHOP-PRIMARY, SHOP-001

        [BsonElement("ShopName")]
        public string ShopName { get; set; } = string.Empty;

        [BsonElement("OwnerName")]
        public string OwnerName { get; set; } = string.Empty;

        [BsonElement("ContactEmail")]
        public string ContactEmail { get; set; } = string.Empty;

        [BsonElement("Phone")]
        public string Phone { get; set; } = string.Empty;

        [BsonElement("Address")]
        public string Address { get; set; } = string.Empty;

        [BsonElement("GSTIN")]
        public string GSTIN { get; set; } = string.Empty;

        [BsonElement("PackageId")]
        public string PackageId { get; set; } = string.Empty;

        [BsonElement("Status")]
        public string Status { get; set; } = "Active"; // Active, Trial, Suspended, Expired

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
