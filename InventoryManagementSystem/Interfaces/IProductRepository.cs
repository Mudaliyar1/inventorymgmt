using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IProductRepository : IBaseRepository<Product>
    {
        Task<Product?> GetByCodeAsync(string code);
        Task<Product?> GetByBarcodeAsync(string barcode);
        Task<IEnumerable<Product>> GetPagedProductsAsync(string? search, string? categoryId, string? sortBy, bool isDescending, int page, int pageSize, string? brand = null, string? modelName = null, string? stockStatus = null, string? statusFilter = null, decimal? minPrice = null, decimal? maxPrice = null, int? minStock = null, int? maxStock = null, string? productSource = null);
        Task<long> GetFilteredCountAsync(string? search, string? categoryId, string? brand = null, string? modelName = null, string? stockStatus = null, string? statusFilter = null, decimal? minPrice = null, decimal? maxPrice = null, int? minStock = null, int? maxStock = null, string? productSource = null);
        Task<(int TotalProducts, int CurrentStockSum, int LowStockCount, int OutOfStockCount)> GetStockMetricsAsync();
    }
}
