using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IRepairRepository _repairRepository;

        public ReturnService(
            IReturnRepository returnRepository,
            IDeviceRepository deviceRepository,
            IProductRepository productRepository,
            ISaleRepository saleRepository,
            IStockTransactionRepository stockTransactionRepository,
            IAuditLogService auditLogService,
            IRepairRepository repairRepository)
        {
            _returnRepository = returnRepository;
            _deviceRepository = deviceRepository;
            _productRepository = productRepository;
            _saleRepository = saleRepository;
            _stockTransactionRepository = stockTransactionRepository;
            _auditLogService = auditLogService;
            _repairRepository = repairRepository;
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

            // 1. INVOICE LOOKUP & ATOMIC VALIDATION
            Sale? originalSale = null;
            SaleItem? matchedSaleItem = null;

            if (!string.IsNullOrWhiteSpace(req.InvoiceNumber))
            {
                var cleanInv = req.InvoiceNumber.Trim();
                originalSale = await _saleRepository.GetByInvoiceNumberAsync(cleanInv);
                if (originalSale == null)
                {
                    return (false, $"Original Invoice #{cleanInv} was not found in the system.", null);
                }

                // Auto populate Customer details if missing
                if (string.IsNullOrWhiteSpace(req.CustomerName)) req.CustomerName = originalSale.CustomerName;
                if (string.IsNullOrWhiteSpace(req.CustomerPhone)) req.CustomerPhone = originalSale.CustomerPhone;
                if (string.IsNullOrWhiteSpace(req.CustomerId)) req.CustomerId = originalSale.CustomerId;

                // Match specific sale item by IMEI or Product
                if (!string.IsNullOrWhiteSpace(req.IMEI))
                {
                    var cleanImei = req.IMEI.Trim();
                    matchedSaleItem = originalSale.Items.FirstOrDefault(i =>
                        string.Equals(i.IMEI1, cleanImei, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(i.IMEI2, cleanImei, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(i.DeviceId, req.DeviceId, StringComparison.OrdinalIgnoreCase));

                    if (matchedSaleItem == null)
                    {
                        return (false, $"IMEI '{cleanImei}' was not sold under Invoice #{cleanInv}.", null);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(req.ProductId))
                {
                    matchedSaleItem = originalSale.Items.FirstOrDefault(i => i.ProductId == req.ProductId && !i.IsReturned);
                }

                // Check Double-Return Protection
                if (matchedSaleItem != null && matchedSaleItem.IsReturned)
                {
                    return (false, $"This item / IMEI '{req.IMEI}' has already been returned for Invoice #{cleanInv}.", null);
                }
            }

            // 2. DEVICE & INVENTORY ROUTING
            Device? device = null;
            string targetStatus = string.IsNullOrWhiteSpace(req.DeviceStatusTarget) ? "Returned" : req.DeviceStatusTarget;

            if (!string.IsNullOrWhiteSpace(req.IMEI))
            {
                device = await _deviceRepository.GetByImeiAsync(req.IMEI.Trim());
                if (device == null)
                {
                    return (false, $"Physical device with IMEI '{req.IMEI}' was not found in inventory.", null);
                }

                // Double Return Protection on Device level
                if (string.Equals(device.Status, "InStock", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(device.Status, "Returned", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(device.Status, "Damaged", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(device.Status, "UnderRepair", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Device IMEI '{req.IMEI}' is currently in status '{device.Status}' (not Sold) and cannot be returned again.", null);
                }

                req.DeviceId = device.Id;
                req.ProductId = device.ProductId;
                req.ProductName = device.ProductName;
                req.ProductCode = device.ProductCode;
                req.CostPrice = device.PurchasePrice;
                req.OriginalSellingPrice = device.SellingPrice;
                if (req.RefundAmount <= 0) req.RefundAmount = device.SellingPrice;

                // DESTINATION ROUTING
                if (targetStatus == "Returned" || targetStatus == "InStock")
                {
                    // Destination A: Returned to Inventory (Restock as InStock)
                    await _deviceRepository.UpdateStatusAsync(device.Id, "InStock");
                    device.CustomerId = string.Empty;
                    device.CustomerName = string.Empty;
                    device.CustomerPhone = string.Empty;
                    device.InvoiceNumber = string.Empty;
                    device.SoldDate = null;
                    await _deviceRepository.UpdateAsync(device.Id, device);

                    // Increase sellable stock
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
                            Reason = $"CUSTOMER RETURN (RESTOCKED INVENTORY) - IMEI: {device.IMEI1} - Return #{req.ReturnNumber}",
                            PreviousStock = prevStock,
                            CurrentStock = prod.CurrentStock,
                            ExecutedBy = executedBy,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                else if (targetStatus == "Damaged")
                {
                    // Destination B: Mark as Damaged / Defective (Do Not Sell)
                    await _deviceRepository.UpdateStatusAsync(device.Id, "Damaged");

                    var prod = await _productRepository.GetByIdAsync(device.ProductId);
                    await _stockTransactionRepository.CreateAsync(new StockTransaction
                    {
                        ProductId = device.ProductId,
                        ProductName = device.ProductName,
                        ProductCode = device.ProductCode,
                        Quantity = 1,
                        Type = "Stock Out",
                        Reason = $"CUSTOMER RETURN (MARKED DAMAGED/DEFECTIVE) - IMEI: {device.IMEI1} - Return #{req.ReturnNumber}",
                        PreviousStock = prod?.CurrentStock ?? 0,
                        CurrentStock = prod?.CurrentStock ?? 0,
                        ExecutedBy = executedBy,
                        Timestamp = DateTime.UtcNow
                    });
                }
                else if (targetStatus == "UnderRepair")
                {
                    // Destination C: Send to Repair Workshop
                    await _deviceRepository.UpdateStatusAsync(device.Id, "UnderRepair");

                    var prod = await _productRepository.GetByIdAsync(device.ProductId);
                    await _stockTransactionRepository.CreateAsync(new StockTransaction
                    {
                        ProductId = device.ProductId,
                        ProductName = device.ProductName,
                        ProductCode = device.ProductCode,
                        Quantity = 1,
                        Type = "Repair",
                        Reason = $"CUSTOMER RETURN (SENT TO REPAIR WORKSHOP) - IMEI: {device.IMEI1} - Return #{req.ReturnNumber}",
                        PreviousStock = prod?.CurrentStock ?? 0,
                        CurrentStock = prod?.CurrentStock ?? 0,
                        ExecutedBy = executedBy,
                        Timestamp = DateTime.UtcNow
                    });

                    // Create Repair Ticket
                    await _repairRepository.CreateAsync(new RepairTicket
                    {
                        TicketNumber = $"REP-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                        CustomerId = req.CustomerId,
                        CustomerName = req.CustomerName,
                        CustomerPhone = req.CustomerPhone,
                        DeviceBrand = device.Brand,
                        DeviceModel = device.ModelName,
                        IMEI = device.IMEI1,
                        ProblemDescription = $"Customer Return Issue: {req.Reason} ({req.Condition})",
                        DeviceCondition = req.Condition,
                        Status = "Received",
                        Notes = $"Linked Return #{req.ReturnNumber}, Invoice #{req.InvoiceNumber}",
                        CreatedBy = executedBy,
                        CreatedDate = DateTime.UtcNow
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(req.ProductId))
            {
                // Accessory Return (Quantity-based)
                var prod = await _productRepository.GetByIdAsync(req.ProductId);
                if (prod != null)
                {
                    req.ProductName = prod.Name;
                    req.ProductCode = prod.Code;
                    req.CostPrice = prod.PurchasePrice;
                    req.OriginalSellingPrice = prod.SellingPrice;

                    if (targetStatus == "Returned" || targetStatus == "InStock")
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
                            Reason = $"CUSTOMER RETURN (RESTOCKED ACCESSORY) - {req.Reason} - Return #{req.ReturnNumber}",
                            PreviousStock = prevStock,
                            CurrentStock = prod.CurrentStock,
                            ExecutedBy = executedBy,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }

            // 3. REVERSE ORIGINAL SALE INVOICE RELATIONSHIP
            if (originalSale != null)
            {
                if (matchedSaleItem != null)
                {
                    matchedSaleItem.IsReturned = true;
                    matchedSaleItem.ReturnedQuantity += req.Quantity;
                    matchedSaleItem.ReturnRecordId = req.ReturnNumber;
                }

                originalSale.TotalRefundedAmount += req.RefundAmount;

                // Check Partial vs Full Return
                bool allItemsReturned = originalSale.Items.All(i => i.IsReturned || i.ReturnedQuantity >= i.Quantity);
                if (allItemsReturned)
                {
                    originalSale.ReturnStatus = "Fully Returned";
                    originalSale.PaymentStatus = "Returned / Refunded";
                }
                else
                {
                    originalSale.ReturnStatus = "Partially Returned";
                }

                await _saleRepository.UpdateAsync(originalSale.Id, originalSale);
            }

            // 4. SAVE RETURN RECORD
            await _returnRepository.CreateAsync(req);

            // 5. AUDIT LOGGING
            await _auditLogService.LogActivityAsync(
                "Customer Return Processed",
                executedBy,
                req.ReturnNumber,
                $"Processed return #{req.ReturnNumber} for Invoice '{req.InvoiceNumber}' (IMEI: {req.IMEI}, Target: {req.DeviceStatusTarget}, Refund: ₹{req.RefundAmount:N2})");

            return (true, $"Return #{req.ReturnNumber} processed successfully. Invoice #{req.InvoiceNumber} status updated to '{originalSale?.ReturnStatus ?? "Returned"}'.", req);
        }

        public async Task<ReturnRecord?> GetReturnByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return await _returnRepository.GetByIdAsync(id);
        }

        public async Task<(bool Success, string Message, ReturnRecord? Record)> UpdateReturnAsync(ReturnRecord req, string updatedBy)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Id)) return (false, "Invalid return record ID.", null);

                var existing = await _returnRepository.GetByIdAsync(req.Id);
                if (existing == null) return (false, "Return record not found.", null);

                existing.CustomerName = req.CustomerName ?? existing.CustomerName;
                existing.CustomerPhone = req.CustomerPhone ?? existing.CustomerPhone;
                existing.Reason = req.Reason ?? existing.Reason;
                existing.RefundAmount = req.RefundAmount;
                existing.Condition = req.Condition ?? existing.Condition;
                existing.Notes = req.Notes ?? existing.Notes;

                await _returnRepository.UpdateAsync(existing.Id, existing);
                await _auditLogService.LogActivityAsync("RETURN_UPDATED", updatedBy, existing.ReturnNumber, $"Updated Return #{existing.ReturnNumber} record details.");
                return (true, $"Return #{existing.ReturnNumber} record updated successfully.", existing);
            }
            catch (Exception ex)
            {
                return (false, "Error updating return record: " + ex.Message, null);
            }
        }

        public async Task<(bool Success, string Message)> DeleteReturnAsync(string id, string deletedBy)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return (false, "Invalid return record ID.");

                var existing = await _returnRepository.GetByIdAsync(id);
                if (existing == null) return (false, "Return record not found.");

                await _returnRepository.DeleteAsync(id);
                await _auditLogService.LogActivityAsync("RETURN_DELETED", deletedBy, existing.ReturnNumber, $"Deleted Return #{existing.ReturnNumber} record.");
                return (true, $"Return #{existing.ReturnNumber} record deleted successfully.");
            }
            catch (Exception ex)
            {
                return (false, "Error deleting return record: " + ex.Message);
            }
        }
    }
}
