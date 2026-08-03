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
        Task<IEnumerable<Product>> GetPagedProductsAsync(string? search, string? categoryId, string? sortBy, bool isDescending, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? categoryId);
    }
}
