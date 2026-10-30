using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class CustomPackageInquiry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("InquiryNumber")]
        public string InquiryNumber { get; set; } = string.Empty; // e.g., INQ-CP-20261026-1001

        [BsonElement("ContactName")]
        public string ContactName { get; set; } = string.Empty;

        [BsonElement("BusinessName")]
        public string BusinessName { get; set; } = string.Empty;

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("Phone")]
        public string Phone { get; set; } = string.Empty;

        [BsonElement("Message")]
        public string Message { get; set; } = string.Empty;

        // Selected quantities
        [BsonElement("AdminCount")]
        public int AdminCount { get; set; }

        [BsonElement("EmployeeCount")]
        public int EmployeeCount { get; set; }

        [BsonElement("ProductCount")]
        public int ProductCount { get; set; }

        [BsonElement("SupplierCount")]
        public int SupplierCount { get; set; }

        [BsonElement("DeviceCount")]
        public int DeviceCount { get; set; }

        // Selected features
        [BsonElement("SelectedFeatureKeys")]
        public List<string> SelectedFeatureKeys { get; set; } = new List<string>();

        [BsonElement("SelectedFeatureNames")]
        public List<string> SelectedFeatureNames { get; set; } = new List<string>();

        // Pricing estimations
        [BsonElement("EstimatedMonthlyPrice")]
        public decimal EstimatedMonthlyPrice { get; set; }

        [BsonElement("EstimatedYearlyPrice")]
        public decimal EstimatedYearlyPrice { get; set; }

        // Snapshot of rates used for calculation
        [BsonElement("PricingConfigSnapshotJson")]
        public string PricingConfigSnapshotJson { get; set; } = string.Empty;

        // Status: New, In Review, Contacted, Quote Sent, Converted, Closed
        [BsonElement("Status")]
        public string Status { get; set; } = "New";

        [BsonElement("AdminNotes")]
        public string AdminNotes { get; set; } = string.Empty;

        [BsonElement("FollowUpHistory")]
        public List<InquiryFollowUp> FollowUpHistory { get; set; } = new List<InquiryFollowUp>();

        [BsonElement("ConvertedTenantId")]
        public string? ConvertedTenantId { get; set; }

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

    [BsonIgnoreExtraElements]
    public class InquiryFollowUp
    {
        [BsonElement("Note")]
        public string Note { get; set; } = string.Empty;

        [BsonElement("CreatedBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [BsonElement("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("StatusChangedTo")]
        public string? StatusChangedTo { get; set; }
    }
}
