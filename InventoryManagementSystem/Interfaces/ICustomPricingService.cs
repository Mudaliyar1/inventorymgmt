using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ICustomPricingService
    {
        Task<CustomPricingConfiguration> GetConfigurationAsync();
        Task SaveConfigurationAsync(CustomPricingConfiguration config, string updatedBy);
        (decimal MonthlyPrice, decimal YearlyPrice, string BreakdownJson) CalculatePrice(
            CustomPricingConfiguration config,
            int admins,
            int employees,
            int products,
            int suppliers,
            int devices,
            List<string>? selectedFeatureKeys);
        Task<CustomPackageInquiry> CreateInquiryAsync(CustomPackageInquiry inquiry);
        Task<List<CustomPackageInquiry>> GetInquiriesAsync(string? search = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null);
        Task<CustomPackageInquiry?> GetInquiryByIdAsync(string id);
        Task<bool> UpdateInquiryStatusAsync(string id, string status, string? note, string updatedBy, string? convertedTenantId = null);
        Task<bool> UpdateConfirmationEmailStatusAsync(string id, string status, string? error);
        Task<(bool Success, string Message)> DeleteInquiryAsync(string id, string deletedBy);
        Task<(int DeletedCount, string Message)> BulkDeleteInquiriesAsync(List<string> ids, string deletedBy);
    }
}
