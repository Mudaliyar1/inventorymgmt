using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class ReturnService : IReturnService
    {
        private readonly IReturnRepository _returnRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IProductRepository _productRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly IStockTransactionRepository _stockTransactionRepository;
        private readonly IAuditLogService _auditLogService;

        public ReturnService(
            IReturnRepository returnRepository,
            IDeviceRepository deviceRepository,
            IProductRepository productRepository,
            ISaleRepository saleRepository,
            IStockTransactionRepository stockTransactionRepository,
            IAuditLogService auditLogService)
        {
            _returnRepository = returnRepository;
            _deviceRepository = deviceRepository;
            _productRepository = productRepository;
            _saleRepository = saleRepository;
            _stockTransactionRepository = stockTransactionRepository;
            _auditLogService = auditLogService;
        }

        public async Task<IEnumerable<ReturnRecord>> GetPagedReturnsAsync(string? search, int page, int pageSize)
        {
            return await _returnRepository.GetPagedReturnsAsync(search, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search)
        {
            return await _returnRepository.GetFilteredCountAsync(search);
        }

        public async Task<(bool Success, string Message, ReturnRecord? Record)> ProcessReturnAsync(ReturnRecord req, string executedBy)
        {
            if (req == null) return (false, "Return request data is missing.", null);
            if (string.IsNullOrWhiteSpace(req.Reason)) return (false, "Return reason is required.", null);

            req.ReturnNumber = $"RET-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            req.ReturnDate = DateTime.UtcNow;
            req.ExecutedBy = executedBy;

            // Handle Device Return (IMEI-based)
            if (!string.IsNullOrWhiteSpace(req.IMEI))
            {
                var device = await _deviceRepository.GetByImeiAsync(req.IMEI);
                if (device == null)
                {
                    return (false, $"Device with IMEI '{req.IMEI}' not found.", null);
                }

                req.DeviceId = device.Id;
                req.ProductId = device.ProductId;
                req.ProductName = device.ProductName;
                req.ProductCode = device.ProductCode;
                req.CustomerName = string.IsNullOrWhiteSpace(req.CustomerName) ? device.CustomerName : req.CustomerName;
                req.CustomerPhone = string.IsNullOrWhiteSpace(req.CustomerPhone) ? device.CustomerPhone : req.CustomerPhone;

                string targetStatus = string.IsNullOrWhiteSpace(req.DeviceStatusTarget) ? "Returned" : req.DeviceStatusTarget;
                await _deviceRepository.UpdateStatusAsync(device.Id, targetStatus);

                // Update product stock if returned to inventory
                if (targetStatus == "Returned" || targetStatus == "InStock")
                {
                    var prod = await _productRepository.GetByIdAsync(device.ProductId);
                    if (prod != null)
                    {
                        int prevStock = prod.CurrentStock;
                        prod.CurrentStock += 1;
                        await _productRepository.UpdateAsync(prod.Id, prod);

                        await _stockTransactionRepository.CreateAsync(new StockTransaction
                        {
                            ProductId = prod.Id,
                            ProductName = prod.Name,
                            ProductCode = prod.Code,
                            Quantity = 1,
                            Type = "Stock In",
                            Reason = $"Customer Return (IMEI: {device.IMEI1}) - {req.Reason}",
                            PreviousStock = prevStock,
                            CurrentStock = prod.CurrentStock,
                            ExecutedBy = executedBy,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(req.ProductId))
            {
                // Accessory Return (Quantity-based)
                var prod = await _productRepository.GetByIdAsync(req.ProductId);
                if (prod != null)
                {
                    int prevStock = prod.CurrentStock;
                    prod.CurrentStock += req.Quantity;
                    await _productRepository.UpdateAsync(prod.Id, prod);

                    await _stockTransactionRepository.CreateAsync(new StockTransaction
                    {
                        ProductId = prod.Id,
                        ProductName = prod.Name,
                        ProductCode = prod.Code,
                        Quantity = req.Quantity,
                        Type = "Stock In",
                        Reason = $"Customer Return - {req.Reason}",
                        PreviousStock = prevStock,
                        CurrentStock = prod.CurrentStock,
                        ExecutedBy = executedBy,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            await _returnRepository.CreateAsync(req);

            await _auditLogService.LogActivityAsync(
                "Product Returned",
                executedBy,
                req.ReturnNumber,
                $"Processed return #{req.ReturnNumber} for customer '{req.CustomerName}' (IMEI: {req.IMEI}, Refund: ₹{req.RefundAmount:N2})");

            return (true, $"Return #{req.ReturnNumber} processed successfully.", req);
        }
    }
}
