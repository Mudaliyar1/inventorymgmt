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

        public async Task<Customer?> GetByPhoneAndNameAsync(string phone, string name)
        {
            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(name)) return null;
            var filter = Builders<Customer>.Filter.And(
                Builders<Customer>.Filter.Eq(c => c.Phone, phone.Trim()),
                Builders<Customer>.Filter.Regex(c => c.Name, new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(name.Trim())}$", "i"))
            );
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Customer>> GetPagedCustomersAsync(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases, int page, int pageSize)
        {
            var filter = BuildFilter(search, hasGstin, minPurchases, maxPurchases);
            return await _collection.Find(filter)
                .SortByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases)
        {
            var filter = BuildFilter(search, hasGstin, minPurchases, maxPurchases);
            return await _collection.CountDocumentsAsync(filter);
        }

        public async Task UpdatePurchasesAsync(string customerId, decimal purchaseAmount)
        {
            var update = Builders<Customer>.Update
                .Inc(c => c.TotalPurchases, purchaseAmount)
                .Set(c => c.UpdatedDate, System.DateTime.UtcNow);
            await _collection.UpdateOneAsync(Builders<Customer>.Filter.Eq(c => c.Id, customerId), update);
        }

        private FilterDefinition<Customer> BuildFilter(string? search, string? hasGstin = null, decimal? minPurchases = null, decimal? maxPurchases = null)
        {
            var builder = Builders<Customer>.Filter;
            var filters = new List<FilterDefinition<Customer>>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                filters.Add(builder.Or(
                    builder.Regex(c => c.Name, new BsonRegularExpression(s, "i")),
                    builder.Regex(c => c.Phone, new BsonRegularExpression(s, "i")),
                    builder.Regex(c => c.Email, new BsonRegularExpression(s, "i")),
                    builder.Regex(c => c.Gstin, new BsonRegularExpression(s, "i")),
                    builder.Regex(c => c.Address, new BsonRegularExpression(s, "i"))
                ));
            }

            if (!string.IsNullOrWhiteSpace(hasGstin))
            {
                if (hasGstin.Equals("Yes", System.StringComparison.OrdinalIgnoreCase))
                {
                    filters.Add(builder.And(
                        builder.Ne(c => c.Gstin, null),
                        builder.Ne(c => c.Gstin, string.Empty)
                    ));
                }
                else if (hasGstin.Equals("No", System.StringComparison.OrdinalIgnoreCase))
                {
                    filters.Add(builder.Or(
                        builder.Eq(c => c.Gstin, null),
                        builder.Eq(c => c.Gstin, string.Empty)
                    ));
                }
            }

            if (minPurchases.HasValue && minPurchases.Value > 0)
            {
                filters.Add(builder.Gte(c => c.TotalPurchases, minPurchases.Value));
            }

            if (maxPurchases.HasValue && maxPurchases.Value > 0)
            {
                filters.Add(builder.Lte(c => c.TotalPurchases, maxPurchases.Value));
            }

            return filters.Any() ? builder.And(filters) : builder.Empty;
        }
    }
}
