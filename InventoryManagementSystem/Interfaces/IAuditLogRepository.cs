using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IAuditLogRepository : IBaseRepository<AuditLog>
    {
        Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count);
    }
}
