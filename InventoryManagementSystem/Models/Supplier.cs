using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class Supplier
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("CompanyName")]
        public string CompanyName { get; set; } = string.Empty;

        [BsonElement("ContactPerson")]
        public string ContactPerson { get; set; } = string.Empty;

        [BsonElement("Phone")]
        public string Phone { get; set; } = string.Empty;

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("Address")]
        public string Address { get; set; } = string.Empty;

        [BsonElement("Gstin")]
        public string Gstin { get; set; } = string.Empty;

        [BsonElement("PaymentTerms")]
        public string PaymentTerms { get; set; } = "Net 30";

        [BsonElement("OutstandingPayable")]
        public decimal OutstandingPayable { get; set; }

        [BsonElement("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedDate")]
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}
