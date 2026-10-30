using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class ContactInquiryService : IContactInquiryService
    {
        private readonly MongoDbContext _context;

        public ContactInquiryService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<ContactInquiry> CreateInquiryAsync(ContactInquiry inquiry)
        {
            var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var count = await _context.ContactInquiries.CountDocumentsAsync(_ => true);
            inquiry.ReferenceNumber = $"CNT-{todayStr}-{(count + 1):D4}";
            inquiry.CreatedAt = DateTime.UtcNow;
            inquiry.UpdatedAt = DateTime.UtcNow;
            inquiry.Status = "New";

            if (inquiry.FollowUpHistory == null) inquiry.FollowUpHistory = new List<InquiryFollowUp>();
            inquiry.FollowUpHistory.Add(new InquiryFollowUp
            {
                Note = "Inquiry received from public contact desk.",
                CreatedBy = "System",
                Timestamp = DateTime.UtcNow,
                StatusChangedTo = "New"
            });

            await _context.ContactInquiries.InsertOneAsync(inquiry);
            return inquiry;
        }

        public async Task<List<ContactInquiry>> GetInquiriesAsync(string? search = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var filter = Builders<ContactInquiry>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filter &= Builders<ContactInquiry>.Filter.Eq(i => i.Status, status);
            }

            if (fromDate.HasValue)
            {
                filter &= Builders<ContactInquiry>.Filter.Gte(i => i.CreatedAt, fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                filter &= Builders<ContactInquiry>.Filter.Lte(i => i.CreatedAt, endOfDay);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchClean = search.Trim();
                var sFilter = Builders<ContactInquiry>.Filter.Or(
                    Builders<ContactInquiry>.Filter.Regex(i => i.ReferenceNumber, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<ContactInquiry>.Filter.Regex(i => i.FullName, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<ContactInquiry>.Filter.Regex(i => i.Email, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<ContactInquiry>.Filter.Regex(i => i.Phone, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<ContactInquiry>.Filter.Regex(i => i.Subject, new MongoDB.Bson.BsonRegularExpression(searchClean, "i")),
                    Builders<ContactInquiry>.Filter.Regex(i => i.BusinessName, new MongoDB.Bson.BsonRegularExpression(searchClean, "i"))
                );
                filter &= sFilter;
            }

            return await _context.ContactInquiries.Find(filter)
                .SortByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<ContactInquiry?> GetInquiryByIdAsync(string id)
        {
            return await _context.ContactInquiries.Find(i => i.Id == id).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateInquiryStatusAsync(string id, string status, string? note, string updatedBy)
        {
            var inquiry = await GetInquiryByIdAsync(id);
            if (inquiry == null) return false;

            inquiry.Status = status;
            inquiry.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(note))
            {
                inquiry.AdminNotes = string.IsNullOrWhiteSpace(inquiry.AdminNotes) ? note : $"{inquiry.AdminNotes}\n[{DateTime.UtcNow:g}] {note}";
            }

            if (inquiry.FollowUpHistory == null) inquiry.FollowUpHistory = new List<InquiryFollowUp>();
            inquiry.FollowUpHistory.Add(new InquiryFollowUp
            {
                Note = note ?? $"Status updated to {status}",
                CreatedBy = updatedBy,
                Timestamp = DateTime.UtcNow,
                StatusChangedTo = status
            });

            var result = await _context.ContactInquiries.ReplaceOneAsync(i => i.Id == id, inquiry);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateConfirmationEmailStatusAsync(string id, string status, string? error)
        {
            var inquiry = await GetInquiryByIdAsync(id);
            if (inquiry == null) return false;

            inquiry.ConfirmationEmailStatus = status;
            if (status == "Sent")
            {
                inquiry.ConfirmationEmailSentAt = DateTime.UtcNow;
                inquiry.ConfirmationEmailError = null;
            }
            else
            {
                inquiry.ConfirmationEmailError = error;
            }
            inquiry.UpdatedAt = DateTime.UtcNow;

            if (inquiry.FollowUpHistory == null) inquiry.FollowUpHistory = new List<InquiryFollowUp>();
            inquiry.FollowUpHistory.Add(new InquiryFollowUp
            {
                Note = status == "Sent" 
                    ? "Confirmation email successfully delivered to customer." 
                    : $"Customer confirmation email failed: {error}",
                CreatedBy = "System",
                Timestamp = DateTime.UtcNow
            });

            var result = await _context.ContactInquiries.ReplaceOneAsync(i => i.Id == id, inquiry);
            return result.ModifiedCount > 0;
        }

        public async Task<(bool Success, string Message)> DeleteInquiryAsync(string id, string deletedBy)
        {
            var inquiry = await GetInquiryByIdAsync(id);
            if (inquiry == null)
            {
                return (false, "Contact message was not found.");
            }

            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Closed", "Resolved", "Completed" };
            if (!allowedStatuses.Contains(inquiry.Status))
            {
                return (false, $"Contact message #{inquiry.ReferenceNumber} cannot be deleted because it is currently '{inquiry.Status}'. Only closed or resolved contact messages can be deleted.");
            }

            var result = await _context.ContactInquiries.DeleteOneAsync(i => i.Id == id);
            if (result.DeletedCount > 0)
            {
                return (true, $"Contact message #{inquiry.ReferenceNumber} ({inquiry.FullName}) has been deleted successfully.");
            }

            return (false, "Failed to delete contact message.");
        }

        public async Task<(int DeletedCount, string Message)> BulkDeleteInquiriesAsync(List<string> ids, string deletedBy)
        {
            if (ids == null || ids.Count == 0)
            {
                return (0, "No contact messages selected for deletion.");
            }

            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Closed", "Resolved", "Completed" };
            var inquiries = await _context.ContactInquiries.Find(i => ids.Contains(i.Id)).ToListAsync();
            var deletableIds = inquiries.Where(i => allowedStatuses.Contains(i.Status)).Select(i => i.Id).ToList();
            var skippedCount = inquiries.Count - deletableIds.Count;

            if (deletableIds.Count == 0)
            {
                return (0, "None of the selected messages could be deleted. Active/pending messages are protected. Only closed or resolved messages can be deleted.");
            }

            var result = await _context.ContactInquiries.DeleteManyAsync(i => deletableIds.Contains(i.Id));
            int deleted = (int)result.DeletedCount;

            string msg = $"{deleted} closed/resolved message record(s) deleted permanently.";
            if (skippedCount > 0)
            {
                msg += $" {skippedCount} active/pending message record(s) were protected and skipped.";
            }

            return (deleted, msg);
        }
    }
}
