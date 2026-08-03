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
    }
}
