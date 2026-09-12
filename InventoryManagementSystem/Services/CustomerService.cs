using InventoryManagementSystem.Helpers;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly IAuditLogService _auditLogService;

        public CustomerService(ICustomerRepository customerRepository, ISaleRepository saleRepository, IAuditLogService auditLogService)
        {
            _customerRepository = customerRepository;
            _saleRepository = saleRepository;
            _auditLogService = auditLogService;
        }

        public async Task<IEnumerable<Customer>> GetAllCustomersAsync()
        {
            return await _customerRepository.GetAllAsync();
        }

        public async Task<Customer?> GetCustomerByIdAsync(string id)
        {
            return await _customerRepository.GetByIdAsync(id);
        }

        public async Task<Customer?> GetCustomerByPhoneAsync(string phone)
        {
            return await _customerRepository.GetByPhoneAsync(phone);
        }

        public async Task<Customer?> GetCustomerByPhoneAndNameAsync(string phone, string name)
        {
            return await _customerRepository.GetByPhoneAndNameAsync(phone, name);
        }

        public async Task<IEnumerable<Customer>> GetPagedCustomersAsync(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases, int page, int pageSize)
        {
            return await _customerRepository.GetPagedCustomersAsync(search, hasGstin, minPurchases, maxPurchases, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases)
        {
            return await _customerRepository.GetFilteredCountAsync(search, hasGstin, minPurchases, maxPurchases);
        }

        public async Task<(bool Success, string Message, Customer? Customer)> SaveCustomerAsync(Customer customer, string executedBy)
        {
            if (customer == null) return (false, "Customer data is missing.", null);
            if (string.IsNullOrWhiteSpace(customer.Name)) return (false, "Customer Name is required.", null);
            if (string.IsNullOrWhiteSpace(customer.Phone)) return (false, "Customer Phone is required.", null);

            // Contact Phone & Email Validation
            if (!ValidationHelper.IsValidPhone(customer.Phone))
            {
                return (false, "Invalid Contact Number format. Phone number must be 10 numeric digits.", null);
            }
            if (!string.IsNullOrWhiteSpace(customer.Email) && !ValidationHelper.IsValidEmail(customer.Email))
            {
                return (false, "Invalid Email address format. Example: customer@domain.com", null);
            }

            var existing = await _customerRepository.GetByPhoneAndNameAsync(customer.Phone, customer.Name);

            if (string.IsNullOrEmpty(customer.Id))
            {
                // Create
                if (existing != null) return (false, $"Customer '{customer.Name}' with phone '{customer.Phone}' already exists.", existing);
                customer.CreatedDate = DateTime.UtcNow;
                customer.UpdatedDate = DateTime.UtcNow;
                await _customerRepository.CreateAsync(customer);

                await _auditLogService.LogActivityAsync(
                    "Customer Added",
                    executedBy,
                    customer.Name,
                    $"Added new customer '{customer.Name}' ({customer.Phone})");

                return (true, "Customer added successfully.", customer);
            }
            else
            {
                // Update
                if (existing != null && existing.Id != customer.Id)
                {
                    return (false, $"Another customer profile for '{customer.Name}' with phone '{customer.Phone}' already exists.", null);
                }

                customer.UpdatedDate = DateTime.UtcNow;
                await _customerRepository.UpdateAsync(customer.Id, customer);

                await _auditLogService.LogActivityAsync(
                    "Customer Updated",
                    executedBy,
                    customer.Name,
                    $"Updated customer profile '{customer.Name}' ({customer.Phone})");

                return (true, "Customer profile updated.", customer);
            }
        }

        public async Task<bool> RecalculateAllCustomerStatsAsync()
        {
            try
            {
                var allSales = await _saleRepository.GetAllAsync();
                var allCustomers = (await _customerRepository.GetAllAsync()).ToList();

                // Group sales by CustomerId
                var salesByCustId = allSales
                    .Where(s => !string.IsNullOrWhiteSpace(s.CustomerId))
                    .GroupBy(s => s.CustomerId)
                    .ToDictionary(g => g.Key, g => g.Sum(s => s.GrandTotal));

                // Also group sales without CustomerId by Phone + Name
                var orphanSales = allSales
                    .Where(s => string.IsNullOrWhiteSpace(s.CustomerId) && !string.IsNullOrWhiteSpace(s.CustomerName))
                    .GroupBy(s => (s.CustomerPhone?.Trim() ?? string.Empty, s.CustomerName.Trim().ToLowerInvariant()));

                foreach (var cust in allCustomers)
                {
                    decimal total = 0m;
                    if (salesByCustId.TryGetValue(cust.Id, out var custTotal))
                    {
                        total += custTotal;
                    }

                    // Also match any orphan sales matching this customer's phone and name
                    var key = (cust.Phone?.Trim() ?? string.Empty, cust.Name.Trim().ToLowerInvariant());
                    var matchedOrphan = orphanSales.FirstOrDefault(g => g.Key.Equals(key));
                    if (matchedOrphan != null)
                    {
                        total += matchedOrphan.Sum(s => s.GrandTotal);
                    }

                    if (cust.TotalPurchases != total)
                    {
                        cust.TotalPurchases = total;
                        cust.UpdatedDate = DateTime.UtcNow;
                        await _customerRepository.UpdateAsync(cust.Id, cust);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool Success, string Message)> DeleteCustomerAsync(string id, string executedBy)
        {
            if (string.IsNullOrWhiteSpace(id)) return (false, "Customer ID is required.");
            var customer = await _customerRepository.GetByIdAsync(id);
            if (customer == null) return (false, "Customer record not found.");

            if (customer.OutstandingBalance > 0)
            {
                return (false, $"Cannot delete customer '{customer.Name}' because they have an outstanding credit balance of ₹{customer.OutstandingBalance:N2}. Please clear the balance first.");
            }

            await _customerRepository.DeleteAsync(id);

            await _auditLogService.LogActivityAsync(
                "Customer Deleted",
                executedBy,
                customer.Name,
                $"Deleted customer profile '{customer.Name}' (Phone: {customer.Phone})");

            return (true, $"Customer '{customer.Name}' deleted successfully.");
        }
    }
}
