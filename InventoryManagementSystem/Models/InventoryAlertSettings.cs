using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    public class InventoryAlertSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("AdminEmail")]
        public string AdminEmail { get; set; } = "admin@sims.com";

        [BsonElement("LowStockThreshold")]
        public int LowStockThreshold { get; set; } = 5;

        [BsonElement("EnableLowStockAlerts")]
        public bool EnableLowStockAlerts { get; set; } = true;

        [BsonElement("EnableOutOfStockAlerts")]
        public bool EnableOutOfStockAlerts { get; set; } = true;

        [BsonElement("EnableStockRestoredAlerts")]
        public bool EnableStockRestoredAlerts { get; set; } = true;

        [BsonElement("EnableDailySummary")]
        public bool EnableDailySummary { get; set; } = false;

        [BsonElement("NotificationFrequency")]
        public string NotificationFrequency { get; set; } = "Immediate"; // Immediate, Daily

        [BsonElement("AlertRecipients")]
        public List<string> AlertRecipients { get; set; } = new List<string>();

        [BsonElement("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedBy")]
        public string UpdatedBy { get; set; } = "System Admin";
    }
}
