using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IReturnService
    {
        Task<IEnumerable<ReturnRecord>> GetPagedReturnsAsync(string? search, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search);
        Task<(bool Success, string Message, ReturnRecord? Record)> ProcessReturnAsync(ReturnRecord returnReq, string executedBy);
    }
}
