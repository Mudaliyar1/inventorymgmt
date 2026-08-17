using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IExchangeRepository : IBaseRepository<ExchangeRecord>
    {
        Task<IEnumerable<ExchangeRecord>> GetPagedExchangesAsync(string? search, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
    }
}
