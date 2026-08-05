using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IStockService
    {
        Task<bool> StockInAsync(string productId, int quantity, string reason, string executedBy);
        Task<bool> StockOutAsync(string productId, int quantity, string reason, string executedBy);
        Task<bool> AdjustStockAsync(string productId, int quantity, string type, string reason, string executedBy);
        Task<IEnumerable<StockTransaction>> GetProductTransactionsAsync(string productId);
        Task<IEnumerable<StockTransaction>> GetRecentHistoryAsync(int count);
        Task<IEnumerable<StockTransaction>> GetPagedHistoryAsync(int page, int pageSize);
        Task<long> GetTotalHistoryCountAsync();

        Task<(IEnumerable<StockTransaction> Items, long TotalCount)> GetFilteredHistoryAsync(
            string? searchTerm,
            string? type,
            string? categoryId,
            string? productId,
            DateTime? startDate,
            DateTime? endDate,
            string? executedBy,
            int page,
            int pageSize);

        Task<bool> DeleteTransactionAsync(string id);
        Task<long> DeleteTransactionsAsync(IEnumerable<string> ids);
    }
}
