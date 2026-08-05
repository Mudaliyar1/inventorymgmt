using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryManagementSystem.Models
{
    public class AuditLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("EmployeeId")]
        public string EmployeeId { get; set; } = string.Empty;

        [BsonElement("EmployeeName")]
        public string EmployeeName { get; set; } = string.Empty;

        [BsonElement("Username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("Action")]
        public string Action { get; set; } = string.Empty;

        [BsonElement("ExecutedBy")]
        public string ExecutedBy { get; set; } = string.Empty; // Username or Full Name

        [BsonElement("Module")]
        public string Module { get; set; } = string.Empty; // Products, Stock, Sales, Employees, System

        [BsonElement("Target")]
        public string Target { get; set; } = string.Empty;

        [BsonElement("PreviousData")]
        public string PreviousData { get; set; } = string.Empty;

        [BsonElement("NewData")]
        public string NewData { get; set; } = string.Empty;

        [BsonElement("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("IpAddress")]
        public string IpAddress { get; set; } = string.Empty;

        [BsonElement("Browser")]
        public string Browser { get; set; } = string.Empty;

        [BsonElement("Device")]
        public string Device { get; set; } = string.Empty;

        [BsonElement("Details")]
        public string Details { get; set; } = string.Empty;
    }
}
