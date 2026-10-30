using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    [BsonIgnoreExtraElements]
    public class PolicyDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("Title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("Slug")]
        public string Slug { get; set; } = string.Empty;

        [BsonElement("Category")]
        public string Category { get; set; } = "General Website Policy";

        [BsonElement("Summary")]
        public string Summary { get; set; } = string.Empty;

        [BsonElement("ContentMarkdown")]
        public string ContentMarkdown { get; set; } = string.Empty;

        [BsonElement("ContentHtml")]
        public string ContentHtml { get; set; } = string.Empty;

        [BsonElement("EffectiveDate")]
        public DateTime? EffectiveDate { get; set; }

        [BsonElement("CreatedAtUtc")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedAtUtc")]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("PublishedAtUtc")]
        public DateTime? PublishedAtUtc { get; set; }

        [BsonElement("CreatedBy")]
        public string CreatedBy { get; set; } = "SuperAdmin";

        [BsonElement("LastUpdatedBy")]
        public string LastUpdatedBy { get; set; } = "SuperAdmin";

        [BsonElement("IsPublished")]
        public bool IsPublished { get; set; } = true;

        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        [BsonElement("DisplayOrder")]
        public int DisplayOrder { get; set; } = 1;

        [BsonElement("ShowInFooter")]
        public bool ShowInFooter { get; set; } = true;

        [BsonElement("RevisionCount")]
        public int RevisionCount { get; set; } = 1;
    }
}
