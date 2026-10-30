using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class PolicyRevision
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("PolicyId")]
        public string PolicyId { get; set; } = string.Empty;

        [BsonElement("RevisionNumber")]
        public int RevisionNumber { get; set; } = 1;

        [BsonElement("Title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("Slug")]
        public string Slug { get; set; } = string.Empty;

        [BsonElement("Category")]
        public string Category { get; set; } = string.Empty;

        [BsonElement("Summary")]
        public string Summary { get; set; } = string.Empty;

        [BsonElement("ContentMarkdown")]
        public string ContentMarkdown { get; set; } = string.Empty;

        [BsonElement("ContentHtml")]
        public string ContentHtml { get; set; } = string.Empty;

        [BsonElement("ChangedAtUtc")]
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("ChangedBy")]
        public string ChangedBy { get; set; } = "SuperAdmin";

        [BsonElement("ChangeType")]
        public string ChangeType { get; set; } = "Created"; // Created, Updated, Published, Unpublished, Activated, Deactivated, Archived

        [BsonElement("Notes")]
        public string Notes { get; set; } = string.Empty;
    }
}
