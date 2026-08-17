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
        private readonly IAuditLogService _auditLogService;

        public CustomerService(ICustomerRepository customerRepository, IAuditLogService auditLogService)
        {
            _customerRepository = customerRepository;
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

        public async Task<IEnumerable<Customer>> GetPagedCustomersAsync(string? search, int page, int pageSize)
        {
            return await _customerRepository.GetPagedCustomersAsync(search, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search)
        {
            return await _customerRepository.GetFilteredCountAsync(search);
        }

        public async Task<(bool Success, string Message, Customer? Customer)> SaveCustomerAsync(Customer customer, string executedBy)
        {
            if (customer == null) return (false, "Customer data is missing.", null);
            if (string.IsNullOrWhiteSpace(customer.Name)) return (false, "Customer Name is required.", null);
            if (string.IsNullOrWhiteSpace(customer.Phone)) return (false, "Customer Phone is required.", null);

            var existing = await _customerRepository.GetByPhoneAsync(customer.Phone);

            if (string.IsNullOrEmpty(customer.Id))
            {
                // Create
                if (existing != null) return (false, $"Customer with phone '{customer.Phone}' already exists.", existing);
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
                    return (false, $"Another customer with phone '{customer.Phone}' already exists.", null);
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
    }
}
