using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IStockTransactionRepository : IBaseRepository<StockTransaction>
    {
        Task<IEnumerable<StockTransaction>> GetRecentTransactionsAsync(int count);
        Task<IEnumerable<StockTransaction>> GetTransactionsByProductIdAsync(string productId);
        Task<IEnumerable<StockTransaction>> GetPagedTransactionsAsync(int page, int pageSize);
        Task<long> GetTotalCountAsync();

        Task<(IEnumerable<StockTransaction> Items, long TotalCount)> GetFilteredTransactionsAsync(
            string? searchTerm,
            string? type,
            string? productId,
            List<string>? matchingProductIds,
            DateTime? startDate,
            DateTime? endDate,
            string? executedBy,
            int page,
            int pageSize);

        Task<long> DeleteManyAsync(IEnumerable<string> ids);
    }
}
