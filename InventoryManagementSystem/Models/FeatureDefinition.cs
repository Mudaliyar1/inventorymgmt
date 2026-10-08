using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryManagementSystem.Models
{
    public class FeatureDefinition
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("FeatureKey")]
        public string FeatureKey { get; set; } = string.Empty; // e.g. "POS_BILLING", "SUPPLIERS"

        [BsonElement("FeatureName")]
        public string FeatureName { get; set; } = string.Empty; // e.g. "POS Billing", "Suppliers"

        [BsonElement("Description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("Category")]
        public string Category { get; set; } = string.Empty; // e.g. "MOBILE SHOP", "DIRECTORY", "INVENTORY & STOCK"

        [BsonElement("ControllerName")]
        public string ControllerName { get; set; } = string.Empty; // e.g. "Sales", "Supplier"

        [BsonElement("ActionName")]
        public string ActionName { get; set; } = "Index"; // e.g. "Create", "Index"

        [BsonElement("Route")]
        public string Route { get; set; } = string.Empty; // e.g. "/Sales/Create", "/Supplier/Index"

        [BsonElement("Icon")]
        public string Icon { get; set; } = "bi-app-indicator"; // e.g. "bi-receipt", "bi-truck"

        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("DisplayOrder")]
        public int DisplayOrder { get; set; } = 1;

        [BsonElement("ParentKey")]
        public string? ParentKey { get; set; } = null; // For sub-features like EMAIL_ALERTS_INVOICE
    }
}
