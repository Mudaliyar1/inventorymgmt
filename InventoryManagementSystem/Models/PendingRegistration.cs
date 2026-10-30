using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class PendingRegistration
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("RegistrationToken")]
        public string RegistrationToken { get; set; } = Guid.NewGuid().ToString("N");

        [BsonElement("ContactEmail")]
        public string ContactEmail { get; set; } = string.Empty;

        [BsonElement("HashedOtp")]
        public string HashedOtp { get; set; } = string.Empty;

        [BsonElement("ShopName")]
        public string ShopName { get; set; } = string.Empty;

        [BsonElement("OwnerName")]
        public string OwnerName { get; set; } = string.Empty;

        [BsonElement("Phone")]
        public string Phone { get; set; } = string.Empty;

        [BsonElement("Address")]
        public string Address { get; set; } = string.Empty;

        [BsonElement("GSTIN")]
        public string GSTIN { get; set; } = string.Empty;

        [BsonElement("PackageId")]
        public string PackageId { get; set; } = string.Empty;

        [BsonElement("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("CreatedAtUtc")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("ExpiresAtUtc")]
        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(10);

        [BsonElement("AttemptCount")]
        public int AttemptCount { get; set; } = 0;

        [BsonElement("MaxAttempts")]
        public int MaxAttempts { get; set; } = 5;

        [BsonElement("ResendCooldownUtc")]
        public DateTime ResendCooldownUtc { get; set; } = DateTime.UtcNow.AddSeconds(60);

        [BsonElement("IsVerified")]
        public bool IsVerified { get; set; } = false;

        [BsonElement("RegistrationMode")]
        public string RegistrationMode { get; set; } = "trial"; // "trial" or "paid"

        [BsonElement("BillingCycle")]
        public string BillingCycle { get; set; } = "Monthly"; // "Monthly" or "Yearly"
    }
}
