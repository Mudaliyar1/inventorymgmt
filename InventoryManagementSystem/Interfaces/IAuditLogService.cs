using System;
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

        Task LogExAsync(
            string action,
            string module,
            string target,
            string details,
            string status = "Success",
            string logLevel = "Information",
            string previousData = "",
            string newData = "",
            string referenceId = "",
            long executionTimeMs = 0);

        Task LogSecurityEventAsync(
            string action,
            string details,
            string status = "Failed",
            string logLevel = "Warning");

        Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int count);
        Task<IEnumerable<AuditLog>> GetLogsByEmployeeAsync(string username, int count = 50);

        Task<(IEnumerable<AuditLog> Items, long TotalCount)> GetFilteredLogsAsync(
            string? keyword,
            string? module,
            string? action,
            string? status,
            string? logLevel,
            string? employee,
            DateTime? startDate,
            DateTime? endDate,
            string? ipAddress,
            string? browser,
            string? device,
            int page = 1,
            int pageSize = 20);

        Task<AuditLogStats> GetLogStatsAsync();
        Task<long> PurgeLogsOlderThanAsync(int days);
        Task<long> ClearAllLogsAsync();

        byte[] ExportLogsCsv(IEnumerable<AuditLog> logs);
        byte[] ExportLogsPdf(IEnumerable<AuditLog> logs);
    }
}
