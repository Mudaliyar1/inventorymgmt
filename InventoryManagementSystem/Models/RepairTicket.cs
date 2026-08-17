using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class RepairTicket
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("TicketNumber")]
        public string TicketNumber { get; set; } = string.Empty;

        [BsonElement("CustomerId")]
        public string CustomerId { get; set; } = string.Empty;

        [BsonElement("CustomerName")]
        public string CustomerName { get; set; } = string.Empty;

        [BsonElement("CustomerPhone")]
        public string CustomerPhone { get; set; } = string.Empty;

        [BsonElement("DeviceBrand")]
        public string DeviceBrand { get; set; } = string.Empty;

        [BsonElement("DeviceModel")]
        public string DeviceModel { get; set; } = string.Empty;

        [BsonElement("IMEI")]
        public string IMEI { get; set; } = string.Empty;

        [BsonElement("ProblemDescription")]
        public string ProblemDescription { get; set; } = string.Empty;

        [BsonElement("DeviceCondition")]
        public string DeviceCondition { get; set; } = string.Empty;

        [BsonElement("AccessoriesReceived")]
        public string AccessoriesReceived { get; set; } = string.Empty; // e.g. Box, Charger, SIM Tray

        [BsonElement("TechnicianName")]
        public string TechnicianName { get; set; } = string.Empty;

        [BsonElement("EstimatedCost")]
        public decimal EstimatedCost { get; set; }

        [BsonElement("FinalCost")]
        public decimal FinalCost { get; set; }

        // Status: Received, Diagnosing, Repairing, Ready, Delivered, Cancelled
        [BsonElement("Status")]
        public string Status { get; set; } = "Received";

        [BsonElement("Notes")]
        public string Notes { get; set; } = string.Empty;

        [BsonElement("CreatedBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [BsonElement("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("CompletedDate")]
        public DateTime? CompletedDate { get; set; }
    }
}
