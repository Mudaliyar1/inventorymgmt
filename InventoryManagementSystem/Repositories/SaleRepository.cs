using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class SaleRepository : BaseRepository<Sale>, ISaleRepository
    {
        public SaleRepository(MongoDbContext context) : base(context, "Sales")
        {
        }

        public async Task<IEnumerable<Sale>> GetRecentSalesAsync(int count)
        {
            return await _collection.Find(FilterDefinition<Sale>.Empty)
                .SortByDescending(s => s.Date)
                .Limit(count)
                .ToListAsync();
        }

        public async Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber)
        {
            var filter = Builders<Sale>.Filter.Eq(s => s.InvoiceNumber, invoiceNumber);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Sale>> GetPagedSalesAsync(int page, int pageSize)
        {
            return await _collection.Find(FilterDefinition<Sale>.Empty)
                .SortByDescending(s => s.Date)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetTotalSalesCountAsync()
        {
            return await _collection.CountDocumentsAsync(FilterDefinition<Sale>.Empty);
        }

        public async Task<IEnumerable<Sale>> GetSalesBetweenDatesAsync(DateTime start, DateTime end)
        {
            var filter = Builders<Sale>.Filter.And(
                Builders<Sale>.Filter.Gte(s => s.Date, start),
                Builders<Sale>.Filter.Lte(s => s.Date, end)
            );
            return await _collection.Find(filter)
                .SortByDescending(s => s.Date)
                .ToListAsync();
        }
    }
}
