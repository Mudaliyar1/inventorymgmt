using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IDeviceService
    {
        Task<Device?> GetDeviceByIdAsync(string id);
        Task<Device?> GetDeviceByImeiAsync(string imei);
        Task<IEnumerable<Device>> GetAvailableDevicesForProductAsync(string productId);
        Task<IEnumerable<Device>> GetPagedDevicesAsync(string? search, string? productId, string? status, string? brand, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? productId, string? status, string? brand);
        Task<(bool Success, string Message, Device? Device)> RegisterDeviceAsync(Device device, string executedBy);
        Task<bool> UpdateDeviceStatusAsync(string deviceId, string status, string? invoiceNumber = null, string? customerId = null, string? customerName = null, string? customerPhone = null);
        Task<bool> ValidateImeiUniquenessAsync(string imei, string? excludeDeviceId = null);
        Task<(bool Success, string Message)> DeleteDeviceAsync(string id, string deletedBy);
    }
}
