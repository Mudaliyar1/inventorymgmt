using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IProductRepository : IBaseRepository<Product>
    {
        Task<Product?> GetByCodeAsync(string code);
        Task<Product?> GetByBarcodeAsync(string barcode);
        Task<IEnumerable<Product>> GetPagedProductsAsync(string? search, string? categoryId, string? sortBy, bool isDescending, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? categoryId);
        Task<(int TotalProducts, int CurrentStockSum, int LowStockCount, int OutOfStockCount)> GetStockMetricsAsync();
    }
}
