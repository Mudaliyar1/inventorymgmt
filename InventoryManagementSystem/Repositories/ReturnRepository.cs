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
        public ReturnRepository(MongoDbContext context, ITenantContext tenantContext) : base(context, "ReturnRecords", tenantContext)
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
            var baseFilter = Builders<ReturnRecord>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                baseFilter = Builders<ReturnRecord>.Filter.Or(
                    Builders<ReturnRecord>.Filter.Regex(r => r.ReturnNumber, new BsonRegularExpression(s, "i")),
                    Builders<ReturnRecord>.Filter.Regex(r => r.InvoiceNumber, new BsonRegularExpression(s, "i")),
                    Builders<ReturnRecord>.Filter.Regex(r => r.IMEI, new BsonRegularExpression(s, "i")),
                    Builders<ReturnRecord>.Filter.Regex(r => r.CustomerName, new BsonRegularExpression(s, "i")),
                    Builders<ReturnRecord>.Filter.Regex(r => r.ProductName, new BsonRegularExpression(s, "i"))
                );
            }
            return Builders<ReturnRecord>.Filter.And(baseFilter, GetTenantFilter());
        }
    }
}
