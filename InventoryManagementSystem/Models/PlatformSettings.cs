using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class PlatformSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Branding & Company Identity
        [BsonElement("PlatformName")]
        public string PlatformName { get; set; } = "SIMS SaaS Platform";

        [BsonElement("LogoUrl")]
        public string LogoUrl { get; set; } = string.Empty;

        [BsonElement("SystemUrl")]
        public string SystemUrl { get; set; } = "https://sims-saas.com";

        [BsonElement("ContactEmail")]
        public string ContactEmail { get; set; } = "support@sims.com";

        [BsonElement("ContactPhone")]
        public string ContactPhone { get; set; } = "+91 98765 43210";

        [BsonElement("OfficeAddress")]
        public string OfficeAddress { get; set; } = "123 Business Hub, Mumbai, India";

        [BsonElement("SupportHours")]
        public string SupportHours { get; set; } = "Mon - Sat: 9:00 AM - 8:00 PM IST";

        // Maintenance Mode for Public SaaS Website
        [BsonElement("WebsiteMaintenanceEnabled")]
        public bool WebsiteMaintenanceEnabled { get; set; } = false;

        [BsonElement("WebsiteMaintenanceMessage")]
        public string WebsiteMaintenanceMessage { get; set; } = "We are currently performing scheduled platform maintenance and upgrades. We will be back online shortly!";

        [BsonElement("WebsiteMaintenanceStartTime")]
        public DateTime? WebsiteMaintenanceStartTime { get; set; }

        [BsonElement("WebsiteMaintenanceEndTime")]
        public DateTime? WebsiteMaintenanceEndTime { get; set; }

        // Specific pages for Website Maintenance (e.g. "HomePage", "Features", "Pricing", "Register", "Contact")
        [BsonElement("MaintenancePages")]
        public List<string> MaintenancePages { get; set; } = new List<string> { "HomePage", "Features", "Pricing", "Register", "Contact" };

        // Maintenance Mode for Shop Dashboard
        [BsonElement("ShopDashboardMaintenanceEnabled")]
        public bool ShopDashboardMaintenanceEnabled { get; set; } = false;

        [BsonElement("ShopDashboardMaintenanceMessage")]
        public string ShopDashboardMaintenanceMessage { get; set; } = "The Shop Admin dashboard is currently undergoing scheduled system optimization. Normal access will resume shortly.";

        [BsonElement("ShopDashboardMaintenanceStartTime")]
        public DateTime? ShopDashboardMaintenanceStartTime { get; set; }

        [BsonElement("ShopDashboardMaintenanceEndTime")]
        public DateTime? ShopDashboardMaintenanceEndTime { get; set; }

        // Offline / Manual Renewal Requests Toggle (When false, only Razorpay online payment is shown)
        [BsonElement("EnableManualRenewalRequests")]
        public bool EnableManualRenewalRequests { get; set; } = false;

        // Payment Recovery & Temporary Checkout Retention Settings
        [BsonElement("TemporaryCheckoutRetentionValue")]
        public int TemporaryCheckoutRetentionValue { get; set; } = 3;

        [BsonElement("TemporaryCheckoutRetentionUnit")]
        public string TemporaryCheckoutRetentionUnit { get; set; } = "Hours"; // "Minutes", "Hours", "Days"

        [BsonElement("PendingVerificationRetentionHours")]
        public int PendingVerificationRetentionHours { get; set; } = 24;

        [BsonElement("EnableCustomerFollowUpMessages")]
        public bool EnableCustomerFollowUpMessages { get; set; } = true;

        [BsonElement("ArchiveBeforeCleanup")]
        public bool ArchiveBeforeCleanup { get; set; } = true;

        [BsonElement("AutoRestoreLatePayments")]
        public bool AutoRestoreLatePayments { get; set; } = true;

        [BsonElement("CustomerReassuranceMessageDurationMinutes")]
        public int CustomerReassuranceMessageDurationMinutes { get; set; } = 60;

        public TimeSpan GetTemporaryCheckoutRetentionTimeSpan()
        {
            int val = TemporaryCheckoutRetentionValue > 0 ? TemporaryCheckoutRetentionValue : 3;
            return (TemporaryCheckoutRetentionUnit?.ToLowerInvariant()) switch
            {
                "minutes" => TimeSpan.FromMinutes(Math.Max(5, val)),
                "days" => TimeSpan.FromDays(Math.Max(1, val)),
                _ => TimeSpan.FromHours(Math.Max(1, val))
            };
        }

        [BsonElement("LastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedBy")]
        public string UpdatedBy { get; set; } = "SuperAdmin";
    }
}
