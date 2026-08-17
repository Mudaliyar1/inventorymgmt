using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<Customer>> GetAllCustomersAsync();
        Task<Customer?> GetCustomerByIdAsync(string id);
        Task<Customer?> GetCustomerByPhoneAsync(string phone);
        Task<IEnumerable<Customer>> GetPagedCustomersAsync(string? search, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
        Task<(bool Success, string Message, Customer? Customer)> SaveCustomerAsync(Customer customer, string executedBy);
    }
}
