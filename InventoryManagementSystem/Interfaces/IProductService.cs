using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(string id);
        Task<Product?> GetProductByCodeAsync(string code);
        Task<Product?> GetProductByBarcodeAsync(string barcode);
        Task CreateProductAsync(Product product);
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(string id);
        Task<IEnumerable<Product>> GetPagedProductsAsync(string? search, string? categoryId, string? sortBy, bool isDescending, int page, int pageSize, string? brand = null, string? modelName = null, string? stockStatus = null, string? statusFilter = null, decimal? minPrice = null, decimal? maxPrice = null, int? minStock = null, int? maxStock = null, string? productSource = null);
        Task<long> GetFilteredCountAsync(string? search, string? categoryId, string? brand = null, string? modelName = null, string? stockStatus = null, string? statusFilter = null, decimal? minPrice = null, decimal? maxPrice = null, int? minStock = null, int? maxStock = null, string? productSource = null);
    }
}
