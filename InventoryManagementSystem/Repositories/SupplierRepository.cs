using MongoDB.Bson;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class SupplierRepository : BaseRepository<Supplier>, ISupplierRepository
    {
        public SupplierRepository(MongoDbContext context) : base(context, "Suppliers")
        {
        }

        public async Task<Supplier?> GetByNameAsync(string companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName)) return null;
            var filter = Builders<Supplier>.Filter.Eq(s => s.CompanyName, companyName.Trim());
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Supplier>> GetPagedSuppliersAsync(string? search, int page, int pageSize)
        {
            var filter = BuildFilter(search);
            return await _collection.Find(filter)
                .SortByDescending(s => s.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search)
        {
            var filter = BuildFilter(search);
            return await _collection.CountDocumentsAsync(filter);
        }

        private FilterDefinition<Supplier> BuildFilter(string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return Builders<Supplier>.Filter.Empty;
            var s = search.Trim();
            return Builders<Supplier>.Filter.Or(
                Builders<Supplier>.Filter.Regex(sup => sup.CompanyName, new BsonRegularExpression(s, "i")),
                Builders<Supplier>.Filter.Regex(sup => sup.ContactPerson, new BsonRegularExpression(s, "i")),
                Builders<Supplier>.Filter.Regex(sup => sup.Phone, new BsonRegularExpression(s, "i")),
                Builders<Supplier>.Filter.Regex(sup => sup.Gstin, new BsonRegularExpression(s, "i"))
            );
        }
    }
}
