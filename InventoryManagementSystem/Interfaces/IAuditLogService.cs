using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Interfaces
{
    public interface IAuditLogService
    {
        Task LogActivityAsync(string action, string executedBy, string target, string details);
        Task LogEmployeeActivityAsync(
            string action,
            string module,
            string target,
            string details,
            string previousData = "",
            string newData = "");
        Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int count);
        Task<IEnumerable<AuditLog>> GetLogsByEmployeeAsync(string username, int count = 50);
    }
}
