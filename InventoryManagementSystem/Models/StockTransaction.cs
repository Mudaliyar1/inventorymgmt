using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class StockTransaction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("ProductId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("Quantity")]
        public int Quantity { get; set; }

        [BsonElement("Type")]
        public string Type { get; set; } = string.Empty; // Stock In, Stock Out, Adjustment

        [BsonElement("Reason")]
        public string Reason { get; set; } = string.Empty; // Purchase, Sale, Damaged, Expired, Lost, Returned, Manual Correction

        [BsonElement("PreviousStock")]
        public int PreviousStock { get; set; }

        [BsonElement("CurrentStock")]
        public int CurrentStock { get; set; }

        [BsonElement("ExecutedBy")]
        public string ExecutedBy { get; set; } = string.Empty; // Username

        [BsonElement("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
