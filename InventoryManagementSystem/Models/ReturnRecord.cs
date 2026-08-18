using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class ReturnRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("ReturnNumber")]
        public string ReturnNumber { get; set; } = string.Empty;

        [BsonElement("InvoiceNumber")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [BsonElement("CustomerId")]
        public string CustomerId { get; set; } = string.Empty;

        [BsonElement("CustomerName")]
        public string CustomerName { get; set; } = string.Empty;

        [BsonElement("CustomerPhone")]
        public string CustomerPhone { get; set; } = string.Empty;

        [BsonElement("DeviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [BsonElement("IMEI")]
        public string IMEI { get; set; } = string.Empty;

        [BsonElement("ProductId")]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("ProductName")]
        public string ProductName { get; set; } = string.Empty;

        [BsonElement("ProductCode")]
        public string ProductCode { get; set; } = string.Empty;

        [BsonElement("Quantity")]
        public int Quantity { get; set; } = 1;

        [BsonElement("ReturnDate")]
        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

        [BsonElement("Reason")]
        public string Reason { get; set; } = string.Empty; // Defective, Wrong Item, Customer Change of Mind

        [BsonElement("Condition")]
        public string Condition { get; set; } = "Unopened"; // Unopened, Opened/Intact, Damaged, Defective

        [BsonElement("RefundAmount")]
        public decimal RefundAmount { get; set; }

        [BsonElement("CostPrice")]
        public decimal CostPrice { get; set; }

        [BsonElement("OriginalSellingPrice")]
        public decimal OriginalSellingPrice { get; set; }

        [BsonElement("Status")]
        public string Status { get; set; } = "Completed"; // Approved, Completed, Cancelled

        [BsonElement("DeviceStatusTarget")]
        public string DeviceStatusTarget { get; set; } = "Returned"; // Returned, Damaged, UnderRepair

        [BsonElement("Notes")]
        public string Notes { get; set; } = string.Empty;

        [BsonElement("ExecutedBy")]
        public string ExecutedBy { get; set; } = string.Empty;
    }
}
