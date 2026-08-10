using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using InventoryManagementSystem.Extensions;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class AuditLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("EmployeeId")]
        [BsonIgnoreIfNull]
        public string EmployeeId { get; set; } = string.Empty;

        [BsonElement("EmployeeName")]
        [BsonIgnoreIfNull]
        public string EmployeeName { get; set; } = string.Empty;

        [BsonElement("Username")]
        [BsonIgnoreIfNull]
        public string Username { get; set; } = string.Empty;

        [BsonElement("UserRole")]
        [BsonIgnoreIfNull]
        public string UserRole { get; set; } = "Staff"; // Admin, Staff, System

        [BsonElement("Action")]
        [BsonIgnoreIfNull]
        public string Action { get; set; } = string.Empty;

        [BsonElement("ExecutedBy")]
        [BsonIgnoreIfNull]
        public string ExecutedBy { get; set; } = string.Empty; // Username or Full Name

        [BsonElement("Module")]
        [BsonIgnoreIfNull]
        public string Module { get; set; } = string.Empty;

        [BsonElement("Target")]
        [BsonIgnoreIfNull]
        public string Target { get; set; } = string.Empty;

        [BsonElement("PreviousData")]
        [BsonIgnoreIfNull]
        public string PreviousData { get; set; } = string.Empty;

        [BsonElement("NewData")]
        [BsonIgnoreIfNull]
        public string NewData { get; set; } = string.Empty;

        [BsonElement("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("IpAddress")]
        [BsonIgnoreIfNull]
        public string IpAddress { get; set; } = string.Empty;

        [BsonElement("Browser")]
        [BsonIgnoreIfNull]
        public string Browser { get; set; } = string.Empty;

        [BsonElement("OperatingSystem")]
        [BsonIgnoreIfNull]
        public string OperatingSystem { get; set; } = string.Empty;

        [BsonElement("Device")]
        [BsonIgnoreIfNull]
        public string Device { get; set; } = string.Empty;

        [BsonElement("DeviceType")]
        [BsonIgnoreIfNull]
        public string DeviceType { get; set; } = "Desktop"; // Desktop, Mobile, Tablet

        [BsonElement("RequestUrl")]
        [BsonIgnoreIfNull]
        public string RequestUrl { get; set; } = string.Empty;

        [BsonElement("HttpMethod")]
        [BsonIgnoreIfNull]
        public string HttpMethod { get; set; } = "GET";

        [BsonElement("Details")]
        [BsonIgnoreIfNull]
        public string Details { get; set; } = string.Empty;

        [BsonElement("ReferenceId")]
        [BsonIgnoreIfNull]
        public string ReferenceId { get; set; } = string.Empty;

        [BsonElement("Status")]
        [BsonIgnoreIfNull]
        public string Status { get; set; } = "Success";

        [BsonElement("LogLevel")]
        [BsonIgnoreIfNull]
        public string LogLevel { get; set; } = "Information";

        [BsonElement("ExecutionTimeMs")]
        [BsonIgnoreIfNull]
        public long ExecutionTimeMs { get; set; } = 0;

        // UI Read-only Computed Properties
        [BsonIgnore]
        public string TimeIstString => Timestamp.ToIstString("MMM d, yyyy HH:mm:ss IST");

        [BsonIgnore]
        public string DateIstString => Timestamp.ToIstString("yyyy-MM-dd");

        [BsonIgnore]
        public string IconClass
        {
            get
            {
                var act = (Action ?? string.Empty).ToLower();
                var mod = (Module ?? string.Empty).ToLower();

                if (act.Contains("login")) return "bi-box-arrow-in-right text-success";
                if (act.Contains("logout")) return "bi-box-arrow-right text-muted";
                if (act.Contains("delete") || act.Contains("remove") || act.Contains("clear")) return "bi-trash-fill text-danger";
                if (act.Contains("create") || act.Contains("add") || act.Contains("seeded")) return "bi-plus-circle-fill text-success";
                if (act.Contains("update") || act.Contains("edit") || act.Contains("adjust") || act.Contains("change")) return "bi-pencil-square text-primary";
                if (act.Contains("lock") || act.Contains("unauthorized") || act.Contains("denied") || act.Contains("suspicious")) return "bi-shield-exclamation text-danger";
                if (act.Contains("stockin")) return "bi-box-arrow-in-down text-success";
                if (act.Contains("stockout")) return "bi-box-arrow-up text-warning";
                if (act.Contains("download") || act.Contains("export") || act.Contains("print")) return "bi-file-earmark-arrow-down text-info";
                if (LogLevel == "Error" || LogLevel == "Critical" || Status == "Failed") return "bi-exclamation-triangle-fill text-danger";
                if (LogLevel == "Warning") return "bi-exclamation-circle-fill text-warning";

                return "bi-check-circle-fill text-success";
            }
        }

        [BsonIgnore]
        public string StatusBadgeClass => (Status ?? "Success") switch
        {
            "Success" => "badge-d badge-green",
            "Warning" => "badge-d badge-yellow",
            "Failed" => "badge-d badge-red",
            "Error" => "badge-d badge-red",
            "Critical" => "badge-d badge-purple",
            _ => "badge-d badge-blue"
        };

        [BsonIgnore]
        public string LogLevelBadgeClass => (LogLevel ?? "Information") switch
        {
            "Information" => "badge-d badge-blue",
            "Success" => "badge-d badge-green",
            "Warning" => "badge-d badge-yellow",
            "Error" => "badge-d badge-red",
            "Critical" => "badge-d badge-purple",
            _ => "badge-d badge-gray"
        };
    }
}
