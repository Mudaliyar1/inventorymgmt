using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IExchangeService
    {
        Task<IEnumerable<ExchangeRecord>> GetPagedExchangesAsync(string? search, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
        Task<(bool Success, string Message, ExchangeRecord? Record)> ProcessExchangeAsync(ExchangeRecord exchangeReq, string executedBy);
    }
}
