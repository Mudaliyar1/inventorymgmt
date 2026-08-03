using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await _productRepository.GetAllAsync();
        }

        public async Task<Product?> GetProductByIdAsync(string id)
        {
            return await _productRepository.GetByIdAsync(id);
        }

        public async Task<Product?> GetProductByCodeAsync(string code)
        {
            return await _productRepository.GetByCodeAsync(code);
        }

        public async Task<Product?> GetProductByBarcodeAsync(string barcode)
        {
            return await _productRepository.GetByBarcodeAsync(barcode);
        }

        public async Task CreateProductAsync(Product product)
        {
            product.CreatedDate = DateTime.UtcNow;
            product.UpdatedDate = DateTime.UtcNow;
            await _productRepository.CreateAsync(product);
        }

        public async Task UpdateProductAsync(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Id))
            {
                Console.WriteLine("[SERVICE DIAGNOSTICS] UpdateProductAsync called with empty/null product.Id — aborting.");
                return;
            }

            product.UpdatedDate = DateTime.UtcNow;
            await _productRepository.UpdateAsync(product.Id, product);
        }

        public async Task<bool> UpdateStockAsync(string id, int newStock)
        {
            var existing = await _productRepository.GetByIdAsync(id);
            if (existing != null)
            {
                existing.CurrentStock = newStock;
                existing.UpdatedDate = DateTime.UtcNow;
                await _productRepository.UpdateAsync(existing.Id, existing);
                return true;
            }
            return false;
        }

        public async Task DeleteProductAsync(string id)
        {
            await _productRepository.DeleteAsync(id);
        }

        public async Task<IEnumerable<Product>> GetPagedProductsAsync(
            string? search, string? categoryId, string? sortBy, bool isDescending, int page, int pageSize)
        {
            return await _productRepository.GetPagedProductsAsync(search, categoryId, sortBy, isDescending, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? categoryId)
        {
            return await _productRepository.GetFilteredCountAsync(search, categoryId);
        }
    }
}
