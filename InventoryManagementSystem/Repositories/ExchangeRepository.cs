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

        public async Task<ExchangeRecord?> GetByImeiAsync(string imei)
        {
            if (string.IsNullOrWhiteSpace(imei)) return null;
            var clean = imei.Trim();
            var filter = Builders<ExchangeRecord>.Filter.Or(
                Builders<ExchangeRecord>.Filter.Eq(e => e.OldImei1, clean),
                Builders<ExchangeRecord>.Filter.Eq(e => e.OldImei2, clean)
            );
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<ExchangeRecord>> GetPagedExchangesAsync(string? search, int page, int pageSize)
        {
            return await GetFilteredExchangesAsync(search, null, null, null, null, page, pageSize);
        }

        public async Task<IEnumerable<ExchangeRecord>> GetFilteredExchangesAsync(string? search, string? brand, string? color, string? condition, string? destinationStatus, int page, int pageSize)
        {
            var filter = BuildAdvancedFilter(search, brand, color, condition, destinationStatus);
            return await _collection.Find(filter)
                .SortByDescending(e => e.Date)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search)
        {
            return await GetFilteredCountExAsync(search, null, null, null, null);
        }

        public async Task<long> GetFilteredCountExAsync(string? search, string? brand, string? color, string? condition, string? destinationStatus)
        {
            var filter = BuildAdvancedFilter(search, brand, color, condition, destinationStatus);
            return await _collection.CountDocumentsAsync(filter);
        }

        private FilterDefinition<ExchangeRecord> BuildAdvancedFilter(string? search, string? brand, string? color, string? condition, string? destinationStatus)
        {
            var filter = Builders<ExchangeRecord>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(brand))
            {
                filter = Builders<ExchangeRecord>.Filter.And(filter, Builders<ExchangeRecord>.Filter.Eq(e => e.OldBrand, brand.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(color))
            {
                filter = Builders<ExchangeRecord>.Filter.And(filter, Builders<ExchangeRecord>.Filter.Eq(e => e.OldColor, color.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(condition))
            {
                filter = Builders<ExchangeRecord>.Filter.And(filter, Builders<ExchangeRecord>.Filter.Eq(e => e.Condition, condition.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(destinationStatus))
            {
                filter = Builders<ExchangeRecord>.Filter.And(filter, Builders<ExchangeRecord>.Filter.Eq(e => e.InventoryDestinationStatus, destinationStatus.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                var searchFilter = Builders<ExchangeRecord>.Filter.Or(
                    Builders<ExchangeRecord>.Filter.Regex(e => e.ExchangeNumber, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.OldImei1, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.OldImei2, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.SerialNumber, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.OldBrand, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.OldModel, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.OldColor, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.CustomerName, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.CustomerPhone, new BsonRegularExpression(s, "i")),
                    Builders<ExchangeRecord>.Filter.Regex(e => e.InvoiceNumber, new BsonRegularExpression(s, "i"))
                );
                filter = Builders<ExchangeRecord>.Filter.And(filter, searchFilter);
            }

            return filter;
        }
    }
}
