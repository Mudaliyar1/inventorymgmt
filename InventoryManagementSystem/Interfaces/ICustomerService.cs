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
        Task<Customer?> GetCustomerByPhoneAndNameAsync(string phone, string name);
        Task<IEnumerable<Customer>> GetPagedCustomersAsync(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases);
        Task<(bool Success, string Message, Customer? Customer)> SaveCustomerAsync(Customer customer, string executedBy);
        Task<(bool Success, string Message)> DeleteCustomerAsync(string id, string executedBy);
        Task<bool> RecalculateAllCustomerStatsAsync();
    }
}
