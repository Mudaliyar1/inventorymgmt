using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Interfaces
{
    public interface IAuditLogService
    {
        Task LogActivityAsync(string action, string executedBy, string target, string details);
        Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int count);
    }
}
