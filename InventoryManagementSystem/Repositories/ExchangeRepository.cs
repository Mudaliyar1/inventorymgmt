using MongoDB.Bson;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class ExchangeRepository : BaseRepository<ExchangeRecord>, IExchangeRepository
    {
        public ExchangeRepository(MongoDbContext context) : base(context, "ExchangeRecords")
        {
        }

        public async Task<IEnumerable<ExchangeRecord>> GetPagedExchangesAsync(string? search, int page, int pageSize)
        {
            var filter = BuildFilter(search);
            return await _collection.Find(filter)
                .SortByDescending(e => e.Date)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search)
        {
            var filter = BuildFilter(search);
            return await _collection.CountDocumentsAsync(filter);
        }

        private FilterDefinition<ExchangeRecord> BuildFilter(string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return Builders<ExchangeRecord>.Filter.Empty;
            var s = search.Trim();
            return Builders<ExchangeRecord>.Filter.Or(
                Builders<ExchangeRecord>.Filter.Regex(e => e.ExchangeNumber, new BsonRegularExpression(s, "i")),
                Builders<ExchangeRecord>.Filter.Regex(e => e.OldImei1, new BsonRegularExpression(s, "i")),
                Builders<ExchangeRecord>.Filter.Regex(e => e.OldBrand, new BsonRegularExpression(s, "i")),
                Builders<ExchangeRecord>.Filter.Regex(e => e.OldModel, new BsonRegularExpression(s, "i")),
                Builders<ExchangeRecord>.Filter.Regex(e => e.CustomerName, new BsonRegularExpression(s, "i")),
                Builders<ExchangeRecord>.Filter.Regex(e => e.InvoiceNumber, new BsonRegularExpression(s, "i"))
            );
        }
    }
}
