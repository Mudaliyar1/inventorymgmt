using MongoDB.Bson;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class CustomerRepository : BaseRepository<Customer>, ICustomerRepository
    {
        public CustomerRepository(MongoDbContext context) : base(context, "Customers")
        {
        }

        public async Task<Customer?> GetByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var filter = Builders<Customer>.Filter.Eq(c => c.Phone, phone.Trim());
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Customer>> GetPagedCustomersAsync(string? search, int page, int pageSize)
        {
            var filter = BuildFilter(search);
            return await _collection.Find(filter)
                .SortByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search)
        {
            var filter = BuildFilter(search);
            return await _collection.CountDocumentsAsync(filter);
        }

        public async Task UpdatePurchasesAsync(string customerId, decimal purchaseAmount)
        {
            var update = Builders<Customer>.Update
                .Inc(c => c.TotalPurchases, purchaseAmount)
                .Set(c => c.UpdatedDate, System.DateTime.UtcNow);
            await _collection.UpdateOneAsync(Builders<Customer>.Filter.Eq(c => c.Id, customerId), update);
        }

        private FilterDefinition<Customer> BuildFilter(string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return Builders<Customer>.Filter.Empty;
            var s = search.Trim();
            return Builders<Customer>.Filter.Or(
                Builders<Customer>.Filter.Regex(c => c.Name, new BsonRegularExpression(s, "i")),
                Builders<Customer>.Filter.Regex(c => c.Phone, new BsonRegularExpression(s, "i")),
                Builders<Customer>.Filter.Regex(c => c.Email, new BsonRegularExpression(s, "i")),
                Builders<Customer>.Filter.Regex(c => c.Gstin, new BsonRegularExpression(s, "i"))
            );
        }
    }
}
