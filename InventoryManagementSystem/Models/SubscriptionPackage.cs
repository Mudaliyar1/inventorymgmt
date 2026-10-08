using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
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

        [BsonElement("MaxEmployees")]
        public int MaxEmployees { get; set; } = 5;

        [BsonElement("MaxProducts")]
        public int MaxProducts { get; set; } = 1000;

        [BsonElement("MaxSuppliers")]
        public int MaxSuppliers { get; set; } = 50;

        [BsonElement("EnabledModules")]
        public List<string> EnabledModules { get; set; } = new List<string>();

        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("DisplayOrder")]
        public int DisplayOrder { get; set; } = 1;

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
