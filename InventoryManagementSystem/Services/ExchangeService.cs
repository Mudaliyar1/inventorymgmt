using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class ExchangeService : IExchangeService
    {
        private readonly IExchangeRepository _exchangeRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IProductRepository _productRepository;
        private readonly IAuditLogService _auditLogService;

        public ExchangeService(
            IExchangeRepository exchangeRepository,
            IDeviceRepository deviceRepository,
            IProductRepository productRepository,
            IAuditLogService auditLogService)
        {
            _exchangeRepository = exchangeRepository;
            _deviceRepository = deviceRepository;
            _productRepository = productRepository;
            _auditLogService = auditLogService;
        }

        public async Task<IEnumerable<ExchangeRecord>> GetPagedExchangesAsync(string? search, int page, int pageSize)
        {
            return await _exchangeRepository.GetPagedExchangesAsync(search, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search)
        {
            return await _exchangeRepository.GetFilteredCountAsync(search);
        }

        public async Task<(bool Success, string Message, ExchangeRecord? Record)> ProcessExchangeAsync(ExchangeRecord req, string executedBy)
        {
            if (req == null) return (false, "Exchange data missing.", null);
            if (string.IsNullOrWhiteSpace(req.OldBrand) || string.IsNullOrWhiteSpace(req.OldModel))
            {
                return (false, "Old phone Brand and Model are required.", null);
            }
            if (req.FinalExchangeValue <= 0)
            {
                return (false, "Final exchange valuation must be greater than zero.", null);
            }

            req.ExchangeNumber = $"EXCH-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            req.Date = DateTime.UtcNow;
            req.ExecutedBy = executedBy;

            // 1. Create a physical Device record for the old exchanged-in phone with status 'Exchanged'
            var oldDevice = new Device
            {
                Brand = req.OldBrand,
                ModelName = req.OldModel,
                Variant = req.OldStorage,
                IMEI1 = req.OldImei1,
                IMEI2 = req.OldImei2,
                PurchasePrice = req.FinalExchangeValue,
                SellingPrice = req.FinalExchangeValue * 1.15m,
                Status = "Exchanged",
                CustomerName = req.CustomerName,
                CustomerPhone = req.CustomerPhone,
                Notes = $"Trade-in Exchanged Phone. Condition: {req.Condition}. Remarks: {req.Remarks}",
                CreatedBy = executedBy,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            // Find or link to a generic 'Exchanged Mobile Phones' Product if exists
            var products = await _productRepository.GetAllAsync();
            Product? genericExchProd = null;
            foreach (var p in products)
            {
                if (p.Name.Contains("Exchange", StringComparison.OrdinalIgnoreCase) || p.ProductType == "Smartphone")
                {
                    genericExchProd = p;
                    break;
                }
            }

            if (genericExchProd != null)
            {
                oldDevice.ProductId = genericExchProd.Id;
                oldDevice.ProductCode = genericExchProd.Code;
                oldDevice.ProductName = $"{req.OldBrand} {req.OldModel} (Pre-owned)";
            }
            else
            {
                oldDevice.ProductName = $"{req.OldBrand} {req.OldModel} (Exchanged)";
                oldDevice.ProductCode = "EXCH-USED";
            }

            await _deviceRepository.CreateAsync(oldDevice);

            await _exchangeRepository.CreateAsync(req);

            await _auditLogService.LogActivityAsync(
                "Mobile Phone Exchanged",
                executedBy,
                req.ExchangeNumber,
                $"Processed trade-in #{req.ExchangeNumber} for old phone '{req.OldBrand} {req.OldModel}' (Valuation: ₹{req.FinalExchangeValue:N2}, Customer: {req.CustomerName})");

            return (true, $"Mobile exchange #{req.ExchangeNumber} recorded successfully. Valuation credit of ₹{req.FinalExchangeValue:N2} applied.", req);
        }
    }
}
