using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class SubscriptionNotificationLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("TenantId")]
        public string TenantId { get; set; } = string.Empty;

        [BsonElement("LicenseId")]
        public string LicenseId { get; set; } = string.Empty;

        [BsonElement("NotificationType")]
        public string NotificationType { get; set; } = string.Empty; // Trial24HourReminder, Trial1HourReminder, TrialExpired, Subscription7DayReminder, Subscription1DayReminder, SubscriptionExpired

        [BsonElement("RecipientEmail")]
        public string RecipientEmail { get; set; } = string.Empty;

        [BsonElement("TriggerAtUtc")]
        public DateTime TriggerAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("SentAtUtc")]
        public DateTime? SentAtUtc { get; set; }

        [BsonElement("Status")]
        public string Status { get; set; } = "Sent"; // Sent, Failed

        [BsonElement("ErrorMessage")]
        public string? ErrorMessage { get; set; }

        [BsonElement("CreatedAtUtc")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
