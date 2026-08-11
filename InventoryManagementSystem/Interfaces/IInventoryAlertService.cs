using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IInventoryAlertService
    {
        Task CheckAndTriggerStockAlertsAsync(Product product, int previousStock, int newStock);
        Task<(bool Success, string Message)> SendTestEmailAsync(string recipientEmail);
        Task<InventoryAlertSettings> GetSettingsAsync();
        Task SaveSettingsAsync(InventoryAlertSettings settings);
        Task<(IEnumerable<InventoryEmailLog> Items, long TotalCount)> GetFilteredLogsAsync(
            string? keyword,
            string? alertType,
            string? status,
            int page = 1,
            int pageSize = 20);
        Task<InventoryAlertDashboardStats> GetDashboardStatsAsync();
        Task<(bool Success, string Message)> ResendEmailLogAsync(string logId);
    }
}
