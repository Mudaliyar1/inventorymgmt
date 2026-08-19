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
        private readonly IAccountValidationService _accountValidationService;

        public SupplierService(
            ISupplierRepository supplierRepository,
            IAuditLogService auditLogService,
            IAccountValidationService accountValidationService)
        {
            _supplierRepository = supplierRepository;
            _auditLogService = auditLogService;
            _accountValidationService = accountValidationService;
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

            // Global Email Uniqueness Check across Admin, Staff, and Suppliers
            if (!string.IsNullOrWhiteSpace(supplier.Email))
            {
                bool isDuplicate = await _accountValidationService.IsEmailAlreadyRegisteredAsync(supplier.Email, excludeSupplierId: supplier.Id);
                if (isDuplicate)
                {
                    return (false, "This email address is already registered with another account.", null);
                }
            }

            // Global Company Name / Username Uniqueness Check
            if (!string.IsNullOrWhiteSpace(supplier.CompanyName))
            {
                bool isNameTaken = await _accountValidationService.IsUsernameAlreadyRegisteredAsync(supplier.CompanyName, excludeSupplierId: supplier.Id);
                if (isNameTaken)
                {
                    return (false, $"The name or identifier '{supplier.CompanyName}' is already in use by another user or supplier account.", null);
                }
            }

            var existing = await _supplierRepository.GetByNameAsync(supplier.CompanyName);

            if (string.IsNullOrEmpty(supplier.Id))
            {
                if (existing != null) return (false, $"Supplier '{supplier.CompanyName}' already exists.", existing);

                if (string.IsNullOrWhiteSpace(supplier.Status)) supplier.Status = "Active";

                if (!string.IsNullOrWhiteSpace(supplier.PasswordHash) && !supplier.PasswordHash.StartsWith("$2"))
                {
                    supplier.PasswordHash = BCrypt.Net.BCrypt.HashPassword(supplier.PasswordHash);
                }

                supplier.CreatedDate = DateTime.UtcNow;
                supplier.UpdatedDate = DateTime.UtcNow;
                await _supplierRepository.CreateAsync(supplier);

                await _auditLogService.LogActivityAsync(
                    "SUPPLIER_CREATED",
                    executedBy,
                    supplier.CompanyName,
                    $"Added new supplier '{supplier.CompanyName}' ({supplier.Email})");

                return (true, "Supplier account added successfully.", supplier);
            }
            else
            {
                if (existing != null && existing.Id != supplier.Id)
                {
                    return (false, $"Another supplier with name '{supplier.CompanyName}' already exists.", null);
                }

                var currentRecord = await _supplierRepository.GetByIdAsync(supplier.Id);
                if (currentRecord != null)
                {
                    // Handle password update logic
                    if (!string.IsNullOrWhiteSpace(supplier.PasswordHash) && !supplier.PasswordHash.StartsWith("$2"))
                    {
                        supplier.PasswordHash = BCrypt.Net.BCrypt.HashPassword(supplier.PasswordHash);
                    }
                    else
                    {
                        supplier.PasswordHash = currentRecord.PasswordHash;
                    }
                }

                if (string.IsNullOrWhiteSpace(supplier.Status)) supplier.Status = currentRecord?.Status ?? "Active";

                supplier.UpdatedDate = DateTime.UtcNow;
                await _supplierRepository.UpdateAsync(supplier.Id, supplier);

                await _auditLogService.LogActivityAsync(
                    "SUPPLIER_UPDATED",
                    executedBy,
                    supplier.CompanyName,
                    $"Updated supplier account '{supplier.CompanyName}'");

                return (true, "Supplier profile updated successfully.", supplier);
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

        public async Task<Supplier?> AuthenticateSupplierAsync(string emailOrUsername, string password)
        {
            if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(password)) return null;

            var input = emailOrUsername.Trim();
            var all = await _supplierRepository.GetAllAsync();
            var supplier = all.FirstOrDefault(s =>
                (!string.IsNullOrEmpty(s.Email) && string.Equals(s.Email.Trim(), input, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.CompanyName) && string.Equals(s.CompanyName.Trim(), input, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.Phone) && string.Equals(s.Phone.Trim(), input, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.ContactPerson) && string.Equals(s.ContactPerson.Trim(), input, StringComparison.OrdinalIgnoreCase)));

            if (supplier == null)
            {
                return null;
            }

            if (string.Equals(supplier.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(supplier.PasswordHash))
            {
                return null;
            }

            bool isValid = false;
            try
            {
                if (supplier.PasswordHash.StartsWith("$2"))
                {
                    isValid = BCrypt.Net.BCrypt.Verify(password, supplier.PasswordHash);
                }
                else if (supplier.PasswordHash == password)
                {
                    isValid = true;
                    // Auto-upgrade plain-text password to BCrypt hash
                    supplier.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                    await _supplierRepository.UpdateAsync(supplier.Id, supplier);
                }
            }
            catch
            {
                if (supplier.PasswordHash == password)
                {
                    isValid = true;
                    supplier.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                    await _supplierRepository.UpdateAsync(supplier.Id, supplier);
                }
            }

            if (!isValid) return null;

            supplier.LastLogin = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(supplier.Status)) supplier.Status = "Active";
            await _supplierRepository.UpdateAsync(supplier.Id, supplier);
            return supplier;
        }
    }
}
