using MongoDB.Bson;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class ReturnRepository : BaseRepository<ReturnRecord>, IReturnRepository
    {
        public ReturnRepository(MongoDbContext context) : base(context, "ReturnRecords")
        {
        }

        public async Task<IEnumerable<ReturnRecord>> GetPagedReturnsAsync(string? search, int page, int pageSize)
        {
            var filter = BuildFilter(search);
            return await _collection.Find(filter)
                .SortByDescending(r => r.ReturnDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search)
        {
            var filter = BuildFilter(search);
            return await _collection.CountDocumentsAsync(filter);
        }

        private FilterDefinition<ReturnRecord> BuildFilter(string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return Builders<ReturnRecord>.Filter.Empty;
            var s = search.Trim();
            return Builders<ReturnRecord>.Filter.Or(
                Builders<ReturnRecord>.Filter.Regex(r => r.ReturnNumber, new BsonRegularExpression(s, "i")),
                Builders<ReturnRecord>.Filter.Regex(r => r.InvoiceNumber, new BsonRegularExpression(s, "i")),
                Builders<ReturnRecord>.Filter.Regex(r => r.IMEI, new BsonRegularExpression(s, "i")),
                Builders<ReturnRecord>.Filter.Regex(r => r.CustomerName, new BsonRegularExpression(s, "i")),
                Builders<ReturnRecord>.Filter.Regex(r => r.ProductName, new BsonRegularExpression(s, "i"))
            );
        }
    }
}
