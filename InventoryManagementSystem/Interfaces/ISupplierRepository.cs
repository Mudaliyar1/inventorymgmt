using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ISupplierRepository : IBaseRepository<Supplier>
    {
        Task<Supplier?> GetByNameAsync(string companyName);
        Task<IEnumerable<Supplier>> GetPagedSuppliersAsync(string? search, string? terms, string? payableStatus, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? terms, string? payableStatus);
    }
}
