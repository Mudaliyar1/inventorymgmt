using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IContactInquiryService
    {
        Task<ContactInquiry> CreateInquiryAsync(ContactInquiry inquiry);
        Task<List<ContactInquiry>> GetInquiriesAsync(string? search = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null);
        Task<ContactInquiry?> GetInquiryByIdAsync(string id);
        Task<bool> UpdateInquiryStatusAsync(string id, string status, string? note, string updatedBy);
        Task<bool> UpdateConfirmationEmailStatusAsync(string id, string status, string? error);
        Task<(bool Success, string Message)> DeleteInquiryAsync(string id, string deletedBy);
        Task<(int DeletedCount, string Message)> BulkDeleteInquiriesAsync(List<string> ids, string deletedBy);
    }
}
