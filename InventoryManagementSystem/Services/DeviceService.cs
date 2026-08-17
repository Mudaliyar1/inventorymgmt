using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly IProductRepository _productRepository;
        private readonly IAuditLogService _auditLogService;

        public DeviceService(
            IDeviceRepository deviceRepository,
            IProductRepository productRepository,
            IAuditLogService auditLogService)
        {
            _deviceRepository = deviceRepository;
            _productRepository = productRepository;
            _auditLogService = auditLogService;
        }

        public async Task<Device?> GetDeviceByIdAsync(string id)
        {
            return await _deviceRepository.GetByIdAsync(id);
        }

        public async Task<Device?> GetDeviceByImeiAsync(string imei)
        {
            return await _deviceRepository.GetByImeiAsync(imei);
        }

        public async Task<IEnumerable<Device>> GetAvailableDevicesForProductAsync(string productId)
        {
            return await _deviceRepository.GetAvailableDevicesForProductAsync(productId);
        }

        public async Task<IEnumerable<Device>> GetPagedDevicesAsync(string? search, string? productId, string? status, string? brand, int page, int pageSize)
        {
            return await _deviceRepository.GetPagedDevicesAsync(search, productId, status, brand, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? productId, string? status, string? brand)
        {
            return await _deviceRepository.GetFilteredCountAsync(search, productId, status, brand);
        }

        public async Task<(bool Success, string Message, Device? Device)> RegisterDeviceAsync(Device device, string executedBy)
        {
            if (device == null) return (false, "Device object cannot be null.", null);

            // Validate IMEI 1
            if (!string.IsNullOrWhiteSpace(device.IMEI1))
            {
                var isExists = await _deviceRepository.IsImeiExistsAsync(device.IMEI1);
                if (isExists) return (false, $"IMEI 1 '{device.IMEI1}' already exists in inventory. Unique IMEI required.", null);
            }

            // Validate IMEI 2
            if (!string.IsNullOrWhiteSpace(device.IMEI2))
            {
                var isExists2 = await _deviceRepository.IsImeiExistsAsync(device.IMEI2);
                if (isExists2) return (false, $"IMEI 2 '{device.IMEI2}' already exists in inventory. Unique IMEI required.", null);
            }

            // Verify Product exists
            var product = await _productRepository.GetByIdAsync(device.ProductId);
            if (product != null)
            {
                device.ProductName = product.Name;
                device.ProductCode = product.Code;
                device.Brand = string.IsNullOrWhiteSpace(device.Brand) ? product.Brand : device.Brand;
                device.ModelName = string.IsNullOrWhiteSpace(device.ModelName) ? product.ModelName : device.ModelName;
                device.Variant = string.IsNullOrWhiteSpace(device.Variant) ? product.Variant : device.Variant;
                device.Color = string.IsNullOrWhiteSpace(device.Color) ? product.Color : device.Color;
                device.PurchasePrice = device.PurchasePrice <= 0 ? product.PurchasePrice : device.PurchasePrice;
                device.SellingPrice = device.SellingPrice <= 0 ? product.SellingPrice : device.SellingPrice;
            }

            device.CreatedBy = executedBy;
            device.CreatedDate = DateTime.UtcNow;
            device.UpdatedDate = DateTime.UtcNow;

            await _deviceRepository.CreateAsync(device);

            // Increment product stock
            if (product != null)
            {
                product.CurrentStock += 1;
                product.UpdatedDate = DateTime.UtcNow;
                await _productRepository.UpdateAsync(product.Id, product);
            }

            await _auditLogService.LogActivityAsync(
                "Device Registered",
                executedBy,
                device.IMEI1,
                $"Registered new physical device '{device.Brand} {device.ModelName}' (IMEI: {device.IMEI1})");

            return (true, "Device registered successfully.", device);
        }

        public async Task<bool> UpdateDeviceStatusAsync(string deviceId, string status, string? invoiceNumber = null, string? customerId = null, string? customerName = null, string? customerPhone = null)
        {
            return await _deviceRepository.UpdateStatusAsync(deviceId, status, invoiceNumber, customerId, customerName, customerPhone);
        }

        public async Task<bool> ValidateImeiUniquenessAsync(string imei, string? excludeDeviceId = null)
        {
            if (string.IsNullOrWhiteSpace(imei)) return true;
            return !(await _deviceRepository.IsImeiExistsAsync(imei, excludeDeviceId));
        }
    }
}
