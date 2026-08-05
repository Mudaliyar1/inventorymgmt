using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class ProductRepository : BaseRepository<Product>, IProductRepository
    {
        public ProductRepository(MongoDbContext context) : base(context, "Products")
        {
        }

        public async Task<Product?> GetByCodeAsync(string code)
        {
            var filter = Builders<Product>.Filter.Eq(p => p.Code, code);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            var filter = Builders<Product>.Filter.Eq(p => p.Barcode, barcode);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Product>> GetPagedProductsAsync(
            string? search, string? categoryId, string? sortBy, bool isDescending, int page, int pageSize)
        {
            var filter = BuildFilter(search, categoryId);
            var query = _collection.Find(filter);

            if (!string.IsNullOrEmpty(sortBy))
            {
                var sortDef = isDescending 
                    ? Builders<Product>.Sort.Descending(sortBy) 
                    : Builders<Product>.Sort.Ascending(sortBy);
                query = query.Sort(sortDef);
            }
            else
            {
                query = query.SortByDescending(p => p.CreatedDate);
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? categoryId)
        {
            var filter = BuildFilter(search, categoryId);
            return await _collection.CountDocumentsAsync(filter);
        }

        public async Task<(int TotalProducts, int CurrentStockSum, int LowStockCount, int OutOfStockCount)> GetStockMetricsAsync()
        {
            var projection = Builders<Product>.Projection
                .Include(p => p.CurrentStock)
                .Include(p => p.MinimumStock)
                .Include(p => p.Status);

            var products = await _collection.Find(FilterDefinition<Product>.Empty)
                .Project<Product>(projection)
                .ToListAsync();

            int totalProducts = products.Count;
            int currentStockSum = 0;
            int lowStockCount = 0;
            int outOfStockCount = 0;

            foreach (var p in products)
            {
                if (p == null) continue;
                currentStockSum += p.CurrentStock;
                if (p.Status == "Active" || string.IsNullOrEmpty(p.Status))
                {
                    if (p.CurrentStock == 0) outOfStockCount++;
                    else if (p.CurrentStock <= p.MinimumStock) lowStockCount++;
                }
            }

            return (totalProducts, currentStockSum, lowStockCount, outOfStockCount);
        }

        private FilterDefinition<Product> BuildFilter(string? search, string? categoryId)
        {
            var filter = Builders<Product>.Filter.Empty;

            if (!string.IsNullOrEmpty(categoryId))
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Eq(p => p.CategoryId, categoryId));
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchFilter = Builders<Product>.Filter.Or(
                    Builders<Product>.Filter.Regex(p => p.Name, new global::MongoDB.Bson.BsonRegularExpression(search, "i")),
                    Builders<Product>.Filter.Regex(p => p.Code, new global::MongoDB.Bson.BsonRegularExpression(search, "i")),
                    Builders<Product>.Filter.Regex(p => p.Barcode, new global::MongoDB.Bson.BsonRegularExpression(search, "i"))
                );
                filter = Builders<Product>.Filter.And(filter, searchFilter);
            }

            return filter;
        }
    }
}
