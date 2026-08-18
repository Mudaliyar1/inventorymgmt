using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IExchangeService
    {
        Task<ExchangeRecord?> GetExchangeByIdAsync(string id);
        Task<IEnumerable<ExchangeRecord>> GetPagedExchangesAsync(string? search, int page, int pageSize);
        Task<IEnumerable<ExchangeRecord>> GetFilteredExchangesAsync(string? search, string? brand, string? color, string? condition, string? destinationStatus, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
        Task<long> GetFilteredCountExAsync(string? search, string? brand, string? color, string? condition, string? destinationStatus);
        Task<(bool Success, string Message, ExchangeRecord? Record)> ProcessExchangeAsync(ExchangeRecord exchangeReq, string executedBy);
        Task<(bool Success, string Message, ExchangeRecord? Record)> UpdateExchangeAsync(ExchangeRecord exchangeReq, string updatedBy);
        Task<(bool Success, string Message)> DeleteExchangeAsync(string id, string deletedBy);
    }
}
