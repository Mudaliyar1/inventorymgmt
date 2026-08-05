using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("FullName")]
        public string FullName { get; set; } = string.Empty;

        [BsonElement("PhoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [BsonElement("Role")]
        public string Role { get; set; } = string.Empty; // Admin or Staff

        [BsonElement("IsLocked")]
        public bool IsLocked { get; set; } = false;

        [BsonElement("ProfilePictureUrl")]
        public string ProfilePictureUrl { get; set; } = string.Empty;

        [BsonElement("ResetToken")]
        public string ResetToken { get; set; } = string.Empty;

        [BsonElement("ResetTokenExpiry")]
        public DateTime? ResetTokenExpiry { get; set; }

        [BsonElement("EmployeeId")]
        public string EmployeeId { get; set; } = string.Empty;

        [BsonElement("Permissions")]
        public List<string> Permissions { get; set; } = new List<string>();

        [BsonElement("PermissionVersion")]
        public int PermissionVersion { get; set; } = 1;

        [BsonElement("LastPermissionUpdated")]
        public DateTime? LastPermissionUpdated { get; set; }

        [BsonElement("LastLogin")]
        public DateTime? LastLogin { get; set; }

        [BsonElement("LastActivity")]
        public DateTime? LastActivity { get; set; }

        [BsonElement("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedDate")]
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}
