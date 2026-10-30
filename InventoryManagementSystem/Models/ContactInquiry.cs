using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class ContactInquiry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("ReferenceNumber")]
        public string ReferenceNumber { get; set; } = string.Empty; // e.g. CNT-20261026-1001

        [BsonElement("FullName")]
        public string FullName { get; set; } = string.Empty;

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("Phone")]
        public string Phone { get; set; } = string.Empty;

        [BsonElement("BusinessName")]
        public string BusinessName { get; set; } = string.Empty;

        [BsonElement("Subject")]
        public string Subject { get; set; } = string.Empty;

        [BsonElement("Message")]
        public string Message { get; set; } = string.Empty;

        // Status: New, In Progress, Responded, Resolved, Closed
        [BsonElement("Status")]
        public string Status { get; set; } = "New";

        [BsonElement("AdminNotes")]
        public string AdminNotes { get; set; } = string.Empty;

        [BsonElement("FollowUpHistory")]
        public List<InquiryFollowUp> FollowUpHistory { get; set; } = new List<InquiryFollowUp>();

        // Confirmation Email Tracking: Pending, Sent, Failed
        [BsonElement("ConfirmationEmailStatus")]
        public string ConfirmationEmailStatus { get; set; } = "Pending";

        [BsonElement("ConfirmationEmailSentAt")]
        public DateTime? ConfirmationEmailSentAt { get; set; }

        [BsonElement("ConfirmationEmailError")]
        public string? ConfirmationEmailError { get; set; }

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
