using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IReturnRepository : IBaseRepository<ReturnRecord>
    {
        Task<IEnumerable<ReturnRecord>> GetPagedReturnsAsync(string? search, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
    }
}
