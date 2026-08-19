using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class Category
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("Description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("Status")]
        public string Status { get; set; } = "Active"; // Active, Inactive

        [BsonElement("SupplierId")]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonIgnoreIfNull]
        [BsonIgnoreIfDefault]
        public string? SupplierId { get; set; }

        [BsonElement("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedDate")]
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}
