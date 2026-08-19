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

        public async Task<IEnumerable<Supplier>> GetPagedSuppliersAsync(string? search, string? terms, string? payableStatus, int page, int pageSize)
        {
            var filter = BuildFilter(search, terms, payableStatus);
            return await _collection.Find(filter)
                .SortByDescending(s => s.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? terms, string? payableStatus)
        {
            var filter = BuildFilter(search, terms, payableStatus);
            return await _collection.CountDocumentsAsync(filter);
        }

        private FilterDefinition<Supplier> BuildFilter(string? search, string? terms, string? payableStatus)
        {
            var builder = Builders<Supplier>.Filter;
            var filters = new List<FilterDefinition<Supplier>>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                var searchFilter = builder.Or(
                    builder.Regex(sup => sup.CompanyName, new BsonRegularExpression(s, "i")),
                    builder.Regex(sup => sup.ContactPerson, new BsonRegularExpression(s, "i")),
                    builder.Regex(sup => sup.Phone, new BsonRegularExpression(s, "i")),
                    builder.Regex(sup => sup.Email, new BsonRegularExpression(s, "i")),
                    builder.Regex(sup => sup.Gstin, new BsonRegularExpression(s, "i")),
                    builder.Regex(sup => sup.Address, new BsonRegularExpression(s, "i"))
                );
                filters.Add(searchFilter);
            }

            if (!string.IsNullOrWhiteSpace(terms))
            {
                filters.Add(builder.Eq(sup => sup.PaymentTerms, terms.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(payableStatus))
            {
                if (payableStatus.Equals("HasBalance", StringComparison.OrdinalIgnoreCase))
                {
                    filters.Add(builder.Gt(sup => sup.OutstandingPayable, 0m));
                }
                else if (payableStatus.Equals("Clear", StringComparison.OrdinalIgnoreCase))
                {
                    filters.Add(builder.Lte(sup => sup.OutstandingPayable, 0m));
                }
            }

            return filters.Any() ? builder.And(filters) : builder.Empty;
        }
    }
}
