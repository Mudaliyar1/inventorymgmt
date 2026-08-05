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

        public async Task<(IEnumerable<Sale> Items, long TotalCount)> GetFilteredSalesAsync(
            string? searchTerm,
            string? customerName,
            DateTime? startDate,
            DateTime? endDate,
            string? cashier,
            int page,
            int pageSize)
        {
            var builder = Builders<Sale>.Filter;
            var filters = new List<FilterDefinition<Sale>>();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var regex = new MongoDB.Bson.BsonRegularExpression(searchTerm.Trim(), "i");
                filters.Add(builder.Or(
                    builder.Regex(s => s.InvoiceNumber, regex),
                    builder.Regex(s => s.CustomerName, regex),
                    builder.Regex(s => s.CustomerPhone, regex),
                    builder.Regex(s => s.CreatedBy, regex)
                ));
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                var regex = new MongoDB.Bson.BsonRegularExpression(customerName.Trim(), "i");
                filters.Add(builder.Or(
                    builder.Regex(s => s.CustomerName, regex),
                    builder.Regex(s => s.CustomerPhone, regex)
                ));
            }

            if (startDate.HasValue)
            {
                filters.Add(builder.Gte(s => s.Date, startDate.Value.ToUniversalTime()));
            }

            if (endDate.HasValue)
            {
                filters.Add(builder.Lte(s => s.Date, endDate.Value.ToUniversalTime()));
            }

            if (!string.IsNullOrWhiteSpace(cashier))
            {
                var regex = new MongoDB.Bson.BsonRegularExpression(cashier.Trim(), "i");
                filters.Add(builder.Regex(s => s.CreatedBy, regex));
            }

            var combinedFilter = filters.Any() ? builder.And(filters) : builder.Empty;

            var totalCount = await _collection.CountDocumentsAsync(combinedFilter);

            var items = await _collection.Find(combinedFilter)
                .SortByDescending(s => s.Date)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<long> DeleteManyAsync(IEnumerable<string> ids)
        {
            if (ids == null || !ids.Any()) return 0;
            var filter = Builders<Sale>.Filter.In(s => s.Id, ids);
            var result = await _collection.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        public async Task<(decimal TodaysSales, decimal MonthlySales, decimal MonthlyProfit)> GetDashboardSalesMetricsAsync(
            DateTime todayUtc,
            DateTime firstOfMonth,
            IDictionary<string, decimal> productPurchasePrices)
        {
            var filter = Builders<Sale>.Filter.Gte(s => s.Date, firstOfMonth);
            var projection = Builders<Sale>.Projection
                .Include(s => s.Date)
                .Include(s => s.GrandTotal)
                .Include(s => s.Items);

            var monthlySalesList = await _collection.Find(filter)
                .Project<Sale>(projection)
                .ToListAsync();

            decimal todaysSales = 0;
            decimal monthlySales = 0;
            decimal monthlyProfit = 0;

            foreach (var sale in monthlySalesList)
            {
                if (sale == null) continue;
                monthlySales += sale.GrandTotal;
                if (sale.Date >= todayUtc)
                {
                    todaysSales += sale.GrandTotal;
                }

                if (sale.Items != null)
                {
                    foreach (var item in sale.Items)
                    {
                        if (item == null) continue;
                        if (productPurchasePrices != null && productPurchasePrices.TryGetValue(item.ProductId, out decimal purchasePrice))
                        {
                            monthlyProfit += (item.SellingPrice - purchasePrice) * item.Quantity;
                        }
                        else
                        {
                            monthlyProfit += (item.SellingPrice * 0.20m) * item.Quantity;
                        }
                    }
                }
            }

            return (todaysSales, monthlySales, monthlyProfit);
        }
    }
}
