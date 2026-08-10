using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public class AuditLogStats
    {
        public long TotalLogs { get; set; }
        public long TodayLogs { get; set; }
        public long SuccessLogs { get; set; }
        public long WarningLogs { get; set; }
        public long ErrorLogs { get; set; }
        public long CriticalLogs { get; set; }
        public long TodayLogins { get; set; }
        public long TodayStockChanges { get; set; }
        public long TodaySales { get; set; }
    }

    public interface IAuditLogRepository : IBaseRepository<AuditLog>
    {
        Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count);

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
        Task<long> DeleteLogsOlderThanAsync(int days);
        Task<long> ClearAllLogsAsync();
        Task EnsureIndexesCreatedAsync();
    }
}
