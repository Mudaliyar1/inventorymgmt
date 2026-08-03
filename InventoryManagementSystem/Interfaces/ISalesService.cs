using InventoryManagementSystem.Models;
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
        byte[] GenerateInvoicePdf(Sale sale);
    }
}
