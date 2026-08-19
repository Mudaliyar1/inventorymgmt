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

        public async Task<long> GetNextInvoiceSequenceAsync()
        {
            var count = await _collection.CountDocumentsAsync(FilterDefinition<Sale>.Empty);
            long nextSeq = count + 1;

            var allSales = await _collection.Find(FilterDefinition<Sale>.Empty)
                .Project(s => s.InvoiceNumber)
                .ToListAsync();

            long maxSeq = 0;
            foreach (var inv in allSales)
            {
                if (string.IsNullOrEmpty(inv)) continue;
                var parts = inv.Split('-');
                if (parts.Length >= 3 && long.TryParse(parts[parts.Length - 1], out long seq))
                {
                    if (seq <= count + 1000 && seq > maxSeq)
                    {
                        maxSeq = seq;
                    }
                }
            }

            return System.Math.Max(nextSeq, maxSeq + 1);
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
            int pageSize,
            string? paymentStatus = null,
            string? paymentMethod = null,
            decimal? minAmount = null,
            decimal? maxAmount = null,
            string? sortBy = null,
            bool isDescending = true)
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

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                filters.Add(builder.Eq(s => s.PaymentStatus, paymentStatus.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(paymentMethod))
            {
                filters.Add(builder.Eq(s => s.PaymentMethod, paymentMethod.Trim()));
            }

            if (minAmount.HasValue)
            {
                filters.Add(builder.Gte(s => s.GrandTotal, minAmount.Value));
            }

            if (maxAmount.HasValue)
            {
                filters.Add(builder.Lte(s => s.GrandTotal, maxAmount.Value));
            }

            var combinedFilter = filters.Any() ? builder.And(filters) : builder.Empty;

            var totalCount = await _collection.CountDocumentsAsync(combinedFilter);

            var query = _collection.Find(combinedFilter);

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                var sortDef = isDescending
                    ? Builders<Sale>.Sort.Descending(sortBy)
                    : Builders<Sale>.Sort.Ascending(sortBy);
                query = query.Sort(sortDef);
            }
            else
            {
                query = query.SortByDescending(s => s.Date);
            }

            var items = await query
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
