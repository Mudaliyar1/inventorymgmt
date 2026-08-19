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
            string? search, string? categoryId, string? sortBy, bool isDescending, int page, int pageSize,
            string? brand = null, string? modelName = null, string? stockStatus = null, string? statusFilter = null,
            decimal? minPrice = null, decimal? maxPrice = null, int? minStock = null, int? maxStock = null, string? productSource = null)
        {
            var filter = BuildFilter(search, categoryId, brand, modelName, stockStatus, statusFilter, minPrice, maxPrice, minStock, maxStock, productSource);
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

        public async Task<long> GetFilteredCountAsync(
            string? search, string? categoryId,
            string? brand = null, string? modelName = null, string? stockStatus = null, string? statusFilter = null,
            decimal? minPrice = null, decimal? maxPrice = null, int? minStock = null, int? maxStock = null, string? productSource = null)
        {
            var filter = BuildFilter(search, categoryId, brand, modelName, stockStatus, statusFilter, minPrice, maxPrice, minStock, maxStock, productSource);
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

        private FilterDefinition<Product> BuildFilter(
            string? search, string? categoryId,
            string? brand = null, string? modelName = null, string? stockStatus = null, string? statusFilter = null,
            decimal? minPrice = null, decimal? maxPrice = null, int? minStock = null, int? maxStock = null, string? productSource = null)
        {
            var filter = Builders<Product>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(productSource))
            {
                if (productSource.Equals("TradeIn", StringComparison.OrdinalIgnoreCase) || productSource.Equals("Exchange", StringComparison.OrdinalIgnoreCase))
                {
                    var tradeInFilter = Builders<Product>.Filter.Or(
                        Builders<Product>.Filter.Regex(p => p.Code, new global::MongoDB.Bson.BsonRegularExpression("^EXCH-", "i")),
                        Builders<Product>.Filter.Regex(p => p.Name, new global::MongoDB.Bson.BsonRegularExpression("Pre-Owned|Trade-In|Exchange", "i"))
                    );
                    filter = Builders<Product>.Filter.And(filter, tradeInFilter);
                }
                else if (productSource.Equals("New", StringComparison.OrdinalIgnoreCase) || productSource.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    var newProductFilter = Builders<Product>.Filter.And(
                        Builders<Product>.Filter.Not(Builders<Product>.Filter.Regex(p => p.Code, new global::MongoDB.Bson.BsonRegularExpression("^EXCH-", "i"))),
                        Builders<Product>.Filter.Not(Builders<Product>.Filter.Regex(p => p.Name, new global::MongoDB.Bson.BsonRegularExpression("Pre-Owned|Trade-In", "i")))
                    );
                    filter = Builders<Product>.Filter.And(filter, newProductFilter);
                }
            }

            if (!string.IsNullOrEmpty(categoryId))
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Eq(p => p.CategoryId, categoryId));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Regex(p => p.Brand, new global::MongoDB.Bson.BsonRegularExpression(brand.Trim(), "i")));
            }

            if (!string.IsNullOrWhiteSpace(modelName))
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Regex(p => p.ModelName, new global::MongoDB.Bson.BsonRegularExpression(modelName.Trim(), "i")));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Eq(p => p.Status, statusFilter.Trim()));
            }

            if (stockStatus == "OutOfStock")
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Lte(p => p.CurrentStock, 0));
            }
            else if (stockStatus == "InStock")
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Gt(p => p.CurrentStock, 0));
            }

            if (minPrice.HasValue)
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Gte(p => p.SellingPrice, minPrice.Value));
            }

            if (maxPrice.HasValue)
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Lte(p => p.SellingPrice, maxPrice.Value));
            }

            if (minStock.HasValue)
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Gte(p => p.CurrentStock, minStock.Value));
            }

            if (maxStock.HasValue)
            {
                filter = Builders<Product>.Filter.And(filter, Builders<Product>.Filter.Lte(p => p.CurrentStock, maxStock.Value));
            }

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.Trim();
                var searchFilter = Builders<Product>.Filter.Or(
                    Builders<Product>.Filter.Regex(p => p.Name, new global::MongoDB.Bson.BsonRegularExpression(s, "i")),
                    Builders<Product>.Filter.Regex(p => p.Code, new global::MongoDB.Bson.BsonRegularExpression(s, "i")),
                    Builders<Product>.Filter.Regex(p => p.Barcode, new global::MongoDB.Bson.BsonRegularExpression(s, "i")),
                    Builders<Product>.Filter.Regex(p => p.Brand, new global::MongoDB.Bson.BsonRegularExpression(s, "i")),
                    Builders<Product>.Filter.Regex(p => p.ModelName, new global::MongoDB.Bson.BsonRegularExpression(s, "i")),
                    Builders<Product>.Filter.Regex(p => p.Variant, new global::MongoDB.Bson.BsonRegularExpression(s, "i"))
                );
                filter = Builders<Product>.Filter.And(filter, searchFilter);
            }

            return filter;
        }
    }
}
