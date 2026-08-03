using MongoDB.Bson;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class StockTransactionRepository : BaseRepository<StockTransaction>, IStockTransactionRepository
    {
        public StockTransactionRepository(MongoDbContext context) : base(context, "StockTransactions")
        {
        }

        public async Task<IEnumerable<StockTransaction>> GetRecentTransactionsAsync(int count)
        {
            return await _collection.Find(FilterDefinition<StockTransaction>.Empty)
                .SortByDescending(t => t.Timestamp)
                .Limit(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<StockTransaction>> GetTransactionsByProductIdAsync(string productId)
        {
            var filter = Builders<StockTransaction>.Filter.Eq(t => t.ProductId, productId);
            return await _collection.Find(filter)
                .SortByDescending(t => t.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<StockTransaction>> GetPagedTransactionsAsync(int page, int pageSize)
        {
            return await _collection.Find(FilterDefinition<StockTransaction>.Empty)
                .SortByDescending(t => t.Timestamp)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetTotalCountAsync()
        {
            return await _collection.CountDocumentsAsync(FilterDefinition<StockTransaction>.Empty);
        }

        public async Task<(IEnumerable<StockTransaction> Items, long TotalCount)> GetFilteredTransactionsAsync(
            string? searchTerm,
            string? type,
            string? productId,
            List<string>? matchingProductIds,
            DateTime? startDate,
            DateTime? endDate,
            string? executedBy,
            int page,
            int pageSize)
        {
            var builder = Builders<StockTransaction>.Filter;
            var filters = new List<FilterDefinition<StockTransaction>>();

            if (!string.IsNullOrWhiteSpace(type))
            {
                filters.Add(builder.Eq(t => t.Type, type));
            }

            if (!string.IsNullOrWhiteSpace(productId))
            {
                filters.Add(builder.Eq(t => t.ProductId, productId));
            }

            if (startDate.HasValue)
            {
                filters.Add(builder.Gte(t => t.Timestamp, startDate.Value.ToUniversalTime()));
            }

            if (endDate.HasValue)
            {
                filters.Add(builder.Lte(t => t.Timestamp, endDate.Value.ToUniversalTime()));
            }

            if (!string.IsNullOrWhiteSpace(executedBy))
            {
                filters.Add(builder.Regex(t => t.ExecutedBy, new BsonRegularExpression(executedBy, "i")));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var regex = new BsonRegularExpression(searchTerm, "i");
                var textFilter = builder.Or(
                    builder.Regex(t => t.Reason, regex),
                    builder.Regex(t => t.ExecutedBy, regex),
                    builder.Regex(t => t.Type, regex)
                );

                if (matchingProductIds != null && matchingProductIds.Any())
                {
                    filters.Add(builder.Or(textFilter, builder.In(t => t.ProductId, matchingProductIds)));
                }
                else
                {
                    filters.Add(textFilter);
                }
            }
            else if (string.IsNullOrWhiteSpace(productId) && matchingProductIds != null)
            {
                // Category filtering without general search term
                filters.Add(builder.In(t => t.ProductId, matchingProductIds));
            }

            var combinedFilter = filters.Any() ? builder.And(filters) : builder.Empty;

            var totalCount = await _collection.CountDocumentsAsync(combinedFilter);

            var items = await _collection.Find(combinedFilter)
                .SortByDescending(t => t.Timestamp)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
