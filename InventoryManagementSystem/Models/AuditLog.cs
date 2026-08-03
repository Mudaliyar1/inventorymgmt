using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryManagementSystem.Models
{
    public class AuditLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Action")]
        public string Action { get; set; } = string.Empty;

        [BsonElement("ExecutedBy")]
        public string ExecutedBy { get; set; } = string.Empty; // Username or User ID

        [BsonElement("Target")]
        public string Target { get; set; } = string.Empty; // e.g. "Product ID: 60e..."

        [BsonElement("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("IpAddress")]
        public string IpAddress { get; set; } = string.Empty;

        [BsonElement("Details")]
        public string Details { get; set; } = string.Empty;
    }
}
