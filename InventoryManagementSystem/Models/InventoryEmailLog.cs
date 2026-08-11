using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class InventoryEmailLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("ProductId")]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("ProductName")]
        public string ProductName { get; set; } = string.Empty;

        [BsonElement("Sku")]
        public string Sku { get; set; } = string.Empty;

        [BsonElement("CategoryName")]
        public string CategoryName { get; set; } = string.Empty;

        [BsonElement("AlertType")]
        public string AlertType { get; set; } = "LowStock"; // LowStock, OutOfStock, StockRestored, TestEmail, DailySummary

        [BsonElement("RecipientEmail")]
        public string RecipientEmail { get; set; } = string.Empty;

        [BsonElement("Subject")]
        public string Subject { get; set; } = string.Empty;

        [BsonElement("Status")]
        public string Status { get; set; } = "Sent"; // Sent, Failed, Pending

        [BsonElement("SentAt")]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        [BsonElement("MessageId")]
        public string MessageId { get; set; } = string.Empty;

        [BsonElement("ApiResponse")]
        public string ApiResponse { get; set; } = string.Empty;

        [BsonElement("ErrorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;

        [BsonElement("RetryCount")]
        public int RetryCount { get; set; } = 0;
    }

    public class InventoryAlertDashboardStats
    {
        public long TotalSent { get; set; }
        public long TodayAlerts { get; set; }
        public long FailedEmails { get; set; }
        public long PendingEmails { get; set; }
        public InventoryEmailLog? LastAlert { get; set; }
        public InventoryEmailLog? LastSuccessfulEmail { get; set; }
    }
}
