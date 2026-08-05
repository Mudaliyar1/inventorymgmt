using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ISalesService
    {
        Task<Sale?> CreateSaleAsync(Sale sale);
        Task<string> GenerateInvoiceNumberAsync();
        Task<Sale?> GetSaleByIdAsync(string id);
        Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber);
        Task<IEnumerable<Sale>> GetPagedSalesAsync(int page, int pageSize);
        Task<long> GetTotalSalesCountAsync();

        Task<(IEnumerable<Sale> Items, long TotalCount)> GetFilteredSalesAsync(
            string? searchTerm,
            string? customerName,
            DateTime? startDate,
            DateTime? endDate,
            string? cashier,
            int page,
            int pageSize);

        Task<bool> DeleteSaleAsync(string id);
        Task<long> DeleteSalesAsync(IEnumerable<string> ids);

        byte[] GenerateInvoicePdf(Sale sale);
    }
}
