using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IExchangeRepository : IBaseRepository<ExchangeRecord>
    {
        Task<ExchangeRecord?> GetByImeiAsync(string imei);
        Task<IEnumerable<ExchangeRecord>> GetPagedExchangesAsync(string? search, int page, int pageSize);
        Task<IEnumerable<ExchangeRecord>> GetFilteredExchangesAsync(string? search, string? brand, string? color, string? condition, string? destinationStatus, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
        Task<long> GetFilteredCountExAsync(string? search, string? brand, string? color, string? condition, string? destinationStatus);
    }
}
