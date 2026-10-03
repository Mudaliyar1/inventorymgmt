using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class PasswordResetRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("TargetId")]
        public string TargetId { get; set; } = string.Empty; // User ID or Supplier ID

        [BsonElement("TargetType")]
        public string TargetType { get; set; } = string.Empty; // "Employee", "Supplier", "Admin"

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("RecipientName")]
        public string RecipientName { get; set; } = string.Empty;

        [BsonElement("TokenHash")]
        public string TokenHash { get; set; } = string.Empty;

        [BsonElement("ExpiresAt")]
        public DateTime ExpiresAt { get; set; }

        [BsonElement("UsedAt")]
        public DateTime? UsedAt { get; set; }

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("CreatedBy")]
        public string CreatedBy { get; set; } = "System";

        [BsonElement("IpAddress")]
        public string? IpAddress { get; set; }

        [BsonIgnore]
        public bool IsActive => UsedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
