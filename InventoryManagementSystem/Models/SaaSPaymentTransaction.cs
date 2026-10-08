using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class SaaSPaymentTransaction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("TenantId")]
        public string TenantId { get; set; } = string.Empty;

        [BsonElement("PackageId")]
        public string PackageId { get; set; } = string.Empty;

        [BsonElement("Amount")]
        public decimal Amount { get; set; }

        [BsonElement("Currency")]
        public string Currency { get; set; } = "INR";

        [BsonElement("TransactionRef")]
        public string TransactionRef { get; set; } = string.Empty;

        [BsonElement("GatewayProvider")]
        public string GatewayProvider { get; set; } = "Razorpay";

        [BsonElement("Status")]
        public string Status { get; set; } = "Success"; // Pending, Success, Failed

        [BsonElement("PaymentDate")]
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }
}
