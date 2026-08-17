using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IDeviceRepository : IBaseRepository<Device>
    {
        Task<Device?> GetByImeiAsync(string imei);
        Task<Device?> GetBySerialAsync(string serialNumber);
        Task<IEnumerable<Device>> GetAvailableDevicesForProductAsync(string productId);
        Task<IEnumerable<Device>> GetDevicesByStatusAsync(string status);
        Task<IEnumerable<Device>> GetPagedDevicesAsync(string? search, string? productId, string? status, string? brand, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? productId, string? status, string? brand);
        Task<bool> IsImeiExistsAsync(string imei, string? excludeId = null);
        Task<bool> UpdateStatusAsync(string deviceId, string status, string? invoiceNumber = null, string? customerId = null, string? customerName = null, string? customerPhone = null);
    }
}
