using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ISupplierOrderRepository : IBaseRepository<SupplierOrder>
    {
        Task<SupplierOrder?> GetByOrderNumberAsync(string orderNumber);
        Task<IEnumerable<SupplierOrder>> GetPagedOrdersAsync(string? search, string? supplierId, string? status, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? supplierId, string? status);
        Task<IEnumerable<SupplierOrder>> GetSupplierOrdersAsync(string supplierId, string? status, int limit = 50);
        Task<string> GetNextOrderNumberAsync();
        Task<Dictionary<string, long>> GetOrderStatusCountsAsync(string? supplierId = null);
    }
}
