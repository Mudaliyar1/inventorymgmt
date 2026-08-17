using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IRepairRepository : IBaseRepository<RepairTicket>
    {
        Task<IEnumerable<RepairTicket>> GetPagedRepairsAsync(string? search, string? status, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? status);
    }
}
