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
