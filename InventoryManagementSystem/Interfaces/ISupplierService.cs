using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ISupplierService
    {
        Task<IEnumerable<Supplier>> GetAllSuppliersAsync();
        Task<Supplier?> GetSupplierByIdAsync(string id);
        Task<IEnumerable<Supplier>> GetPagedSuppliersAsync(string? search, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
        Task<(bool Success, string Message, Supplier? Supplier)> SaveSupplierAsync(Supplier supplier, string executedBy);
    }
}
