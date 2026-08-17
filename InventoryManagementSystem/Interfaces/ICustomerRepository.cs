using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ICustomerRepository : IBaseRepository<Customer>
    {
        Task<Customer?> GetByPhoneAsync(string phone);
        Task<IEnumerable<Customer>> GetPagedCustomersAsync(string? search, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
        Task UpdatePurchasesAsync(string customerId, decimal purchaseAmount);
    }
}
