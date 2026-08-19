using InventoryManagementSystem.Helpers;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly ISupplierRepository _supplierRepository;
        private readonly IAuditLogService _auditLogService;

        public SupplierService(ISupplierRepository supplierRepository, IAuditLogService auditLogService)
        {
            _supplierRepository = supplierRepository;
            _auditLogService = auditLogService;
        }

        public async Task<IEnumerable<Supplier>> GetAllSuppliersAsync()
        {
            return await _supplierRepository.GetAllAsync();
        }

        public async Task<Supplier?> GetSupplierByIdAsync(string id)
        {
            return await _supplierRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Supplier>> GetPagedSuppliersAsync(string? search, string? terms, string? payableStatus, int page, int pageSize)
        {
            return await _supplierRepository.GetPagedSuppliersAsync(search, terms, payableStatus, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? terms, string? payableStatus)
        {
            return await _supplierRepository.GetFilteredCountAsync(search, terms, payableStatus);
        }

        public async Task<(bool Success, string Message, Supplier? Supplier)> SaveSupplierAsync(Supplier supplier, string executedBy)
        {
            if (supplier == null) return (false, "Supplier data is missing.", null);
            if (string.IsNullOrWhiteSpace(supplier.CompanyName)) return (false, "Company Name is required.", null);

            // Contact Phone & Email Validation
            if (!string.IsNullOrWhiteSpace(supplier.Phone) && !ValidationHelper.IsValidPhone(supplier.Phone))
            {
                return (false, "Invalid Contact Number format. Phone number must be 10 numeric digits.", null);
            }
            if (!string.IsNullOrWhiteSpace(supplier.Email) && !ValidationHelper.IsValidEmail(supplier.Email))
            {
                return (false, "Invalid Email address format. Example: supplier@domain.com", null);
            }

            var existing = await _supplierRepository.GetByNameAsync(supplier.CompanyName);

            if (string.IsNullOrEmpty(supplier.Id))
            {
                if (existing != null) return (false, $"Supplier '{supplier.CompanyName}' already exists.", existing);
                supplier.CreatedDate = DateTime.UtcNow;
                supplier.UpdatedDate = DateTime.UtcNow;
                await _supplierRepository.CreateAsync(supplier);

                await _auditLogService.LogActivityAsync(
                    "Supplier Added",
                    executedBy,
                    supplier.CompanyName,
                    $"Added new supplier '{supplier.CompanyName}'");

                return (true, "Supplier added successfully.", supplier);
            }
            else
            {
                if (existing != null && existing.Id != supplier.Id)
                {
                    return (false, $"Another supplier with name '{supplier.CompanyName}' already exists.", null);
                }

                supplier.UpdatedDate = DateTime.UtcNow;
                await _supplierRepository.UpdateAsync(supplier.Id, supplier);

                await _auditLogService.LogActivityAsync(
                    "Supplier Updated",
                    executedBy,
                    supplier.CompanyName,
                    $"Updated supplier '{supplier.CompanyName}'");

                return (true, "Supplier profile updated.", supplier);
            }
        }

        public async Task<(bool Success, string Message)> DeleteSupplierAsync(string id, string executedBy)
        {
            if (string.IsNullOrWhiteSpace(id)) return (false, "Supplier ID is required.");
            var supplier = await _supplierRepository.GetByIdAsync(id);
            if (supplier == null) return (false, "Supplier record not found.");

            await _supplierRepository.DeleteAsync(id);
            await _auditLogService.LogActivityAsync(
                "Supplier Deleted",
                executedBy,
                supplier.CompanyName,
                $"Deleted supplier '{supplier.CompanyName}'");

            return (true, $"Supplier '{supplier.CompanyName}' deleted successfully.");
        }
    }
}
