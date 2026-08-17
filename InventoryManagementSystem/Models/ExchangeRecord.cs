using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class ExchangeRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("ExchangeNumber")]
        public string ExchangeNumber { get; set; } = string.Empty;

        // Old Exchanged Phone Info
        [BsonElement("OldBrand")]
        public string OldBrand { get; set; } = string.Empty;

        [BsonElement("OldModel")]
        public string OldModel { get; set; } = string.Empty;

        [BsonElement("OldImei1")]
        public string OldImei1 { get; set; } = string.Empty;

        [BsonElement("OldImei2")]
        public string OldImei2 { get; set; } = string.Empty;

        [BsonElement("OldStorage")]
        public string OldStorage { get; set; } = string.Empty;

        [BsonElement("OldRam")]
        public string OldRam { get; set; } = string.Empty;

        [BsonElement("Condition")]
        public string Condition { get; set; } = "Good"; // Flawless, Good, Fair, Damaged

        [BsonElement("EstimatedValue")]
        public decimal EstimatedValue { get; set; }

        [BsonElement("FinalExchangeValue")]
        public decimal FinalExchangeValue { get; set; }

        [BsonElement("Remarks")]
        public string Remarks { get; set; } = string.Empty;

        // Purchased New Device Info
        [BsonElement("NewDeviceId")]
        public string NewDeviceId { get; set; } = string.Empty;

        [BsonElement("NewImei")]
        public string NewImei { get; set; } = string.Empty;

        [BsonElement("NewProductId")]
        public string NewProductId { get; set; } = string.Empty;

        [BsonElement("NewProductName")]
        public string NewProductName { get; set; } = string.Empty;

        [BsonElement("InvoiceNumber")]
        public string InvoiceNumber { get; set; } = string.Empty;

        // Customer & Employee
        [BsonElement("CustomerId")]
        public string CustomerId { get; set; } = string.Empty;

        [BsonElement("CustomerName")]
        public string CustomerName { get; set; } = string.Empty;

        [BsonElement("CustomerPhone")]
        public string CustomerPhone { get; set; } = string.Empty;

        [BsonElement("ExecutedBy")]
        public string ExecutedBy { get; set; } = string.Empty;

        [BsonElement("Date")]
        public DateTime Date { get; set; } = DateTime.UtcNow;
    }
}
