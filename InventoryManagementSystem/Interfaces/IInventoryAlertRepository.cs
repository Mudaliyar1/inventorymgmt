using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IInventoryAlertRepository
    {
        Task<InventoryAlertSettings> GetSettingsAsync();
        Task SaveSettingsAsync(InventoryAlertSettings settings);
        Task CreateEmailLogAsync(InventoryEmailLog log);
        Task UpdateEmailLogAsync(InventoryEmailLog log);
        Task<InventoryEmailLog?> GetEmailLogByIdAsync(string id);
        Task<(IEnumerable<InventoryEmailLog> Items, long TotalCount)> GetFilteredLogsAsync(
            string? keyword,
            string? alertType,
            string? status,
            int page = 1,
            int pageSize = 20);
        Task<InventoryAlertDashboardStats> GetDashboardStatsAsync();
    }
}
