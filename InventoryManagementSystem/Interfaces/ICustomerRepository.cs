using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ICustomerRepository : IBaseRepository<Customer>
    {
        Task<Customer?> GetByPhoneAsync(string phone);
        Task<Customer?> GetByPhoneAndNameAsync(string phone, string name);
        Task<IEnumerable<Customer>> GetPagedCustomersAsync(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases);
        Task UpdatePurchasesAsync(string customerId, decimal purchaseAmount);
    }
}
