using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ISaleRepository : IBaseRepository<Sale>
    {
        Task<IEnumerable<Sale>> GetRecentSalesAsync(int count);
        Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber);
        Task<IEnumerable<Sale>> GetPagedSalesAsync(int page, int pageSize);
        Task<long> GetTotalSalesCountAsync();
        Task<IEnumerable<Sale>> GetSalesBetweenDatesAsync(DateTime start, DateTime end);

        Task<(IEnumerable<Sale> Items, long TotalCount)> GetFilteredSalesAsync(
            string? searchTerm,
            string? customerName,
            DateTime? startDate,
            DateTime? endDate,
            string? cashier,
            int page,
            int pageSize);

        Task<long> DeleteManyAsync(IEnumerable<string> ids);

        Task<(decimal TodaysSales, decimal MonthlySales, decimal MonthlyProfit)> GetDashboardSalesMetricsAsync(DateTime todayUtc, DateTime firstOfMonth, IDictionary<string, decimal> productPurchasePrices);
    }
}
