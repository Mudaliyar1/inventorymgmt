using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class StockService : IStockService
    {
        private readonly IProductRepository _productRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IStockTransactionRepository _transactionRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly IInventoryAlertService _inventoryAlertService;

        public StockService(
            IProductRepository productRepository,
            IDeviceRepository deviceRepository,
            IStockTransactionRepository transactionRepository,
            INotificationRepository notificationRepository,
            IEmailService emailService,
            IUserRepository userRepository,
            IInventoryAlertService inventoryAlertService)
        {
            _productRepository = productRepository;
            _deviceRepository = deviceRepository;
            _transactionRepository = transactionRepository;
            _notificationRepository = notificationRepository;
            _emailService = emailService;
            _userRepository = userRepository;
            _inventoryAlertService = inventoryAlertService;
        }

        public async Task<bool> StockInAsync(string productId, int quantity, string reason, string executedBy)
        {
            return await AdjustStockAsync(productId, quantity, "Stock In", reason, executedBy);
        }

        public async Task<bool> StockOutAsync(string productId, int quantity, string reason, string executedBy)
        {
            return await AdjustStockAsync(productId, quantity, "Stock Out", reason, executedBy);
        }

        public async Task<(bool Success, string Message)> StockInDeviceAsync(Device device, string executedBy)
        {
            if (device == null || string.IsNullOrWhiteSpace(device.ProductId))
            {
                return (false, "Invalid device or product selection.");
            }

            // Sanitize IMEI2 and SerialNumber so empty strings are stored as null (avoiding duplicate empty string index error)
            device.IMEI1 = device.IMEI1?.Trim() ?? string.Empty;
            device.IMEI2 = string.IsNullOrWhiteSpace(device.IMEI2) ? null : device.IMEI2.Trim();
            device.SerialNumber = string.IsNullOrWhiteSpace(device.SerialNumber) ? null : device.SerialNumber.Trim();

            // Validate IMEI numeric format
            if (!string.IsNullOrWhiteSpace(device.IMEI1) && !InventoryManagementSystem.Helpers.ValidationHelper.IsValidImei(device.IMEI1))
            {
                return (false, $"Invalid IMEI 1 format '{device.IMEI1}'. IMEI must be a 14 to 16 digit number.");
            }
            if (!string.IsNullOrWhiteSpace(device.IMEI2) && !InventoryManagementSystem.Helpers.ValidationHelper.IsValidImei(device.IMEI2))
            {
                return (false, $"Invalid IMEI 2 format '{device.IMEI2}'. IMEI must be a 14 to 16 digit number.");
            }

            // Check IMEI 1
            if (!string.IsNullOrWhiteSpace(device.IMEI1) && await _deviceRepository.IsImeiExistsAsync(device.IMEI1))
            {
                return (false, $"IMEI 1 '{device.IMEI1}' already exists in database. Unique IMEI required.");
            }
            // Check IMEI 2
            if (!string.IsNullOrWhiteSpace(device.IMEI2) && await _deviceRepository.IsImeiExistsAsync(device.IMEI2))
            {
                return (false, $"IMEI 2 '{device.IMEI2}' already exists in database. Unique IMEI required.");
            }

            var product = await _productRepository.GetByIdAsync(device.ProductId);
            if (product == null) return (false, "Target product model not found.");

            device.ProductName = product.Name;
            device.ProductCode = product.Code;
            device.Brand = string.IsNullOrWhiteSpace(device.Brand) ? product.Brand : device.Brand;
            device.ModelName = string.IsNullOrWhiteSpace(device.ModelName) ? product.ModelName : device.ModelName;
            device.Variant = string.IsNullOrWhiteSpace(device.Variant) ? product.Variant : device.Variant;
            device.Color = string.IsNullOrWhiteSpace(device.Color) ? product.Color : device.Color;
            device.PurchasePrice = device.PurchasePrice <= 0 ? product.PurchasePrice : device.PurchasePrice;
            device.SellingPrice = device.SellingPrice <= 0 ? product.SellingPrice : device.SellingPrice;
            device.Status = "InStock";
            device.CreatedBy = executedBy;
            device.CreatedDate = DateTime.UtcNow;
            device.UpdatedDate = DateTime.UtcNow;

            try
            {
                await _deviceRepository.CreateAsync(device);
            }
            catch (MongoDB.Driver.MongoWriteException ex) when (ex.WriteError.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
            {
                return (false, $"Duplicate IMEI or Serial Number error: {ex.WriteError.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to save device IMEI '{device.IMEI1}': {ex.Message}");
            }

            // Increase product stock & log transaction
            await AdjustStockAsync(product.Id, 1, "Stock In", $"Received Physical Device (IMEI: {device.IMEI1}, Supplier: {device.SupplierName})", executedBy);

            return (true, $"Stock In completed for IMEI {device.IMEI1}.");
        }

        public async Task<(bool Success, string Message)> StockOutDeviceAsync(string deviceId, string statusReason, string executedBy)
        {
            var device = await _deviceRepository.GetByIdAsync(deviceId);
            if (device == null) return (false, "Device record not found.");
            if (device.Status != "InStock") return (false, $"Device is currently in status '{device.Status}' and cannot be issued out.");

            string newStatus = string.IsNullOrWhiteSpace(statusReason) ? "Sold" : statusReason;
            await _deviceRepository.UpdateStatusAsync(device.Id, newStatus);

            var product = await _productRepository.GetByIdAsync(device.ProductId);
            if (product != null)
            {
                await AdjustStockAsync(product.Id, 1, "Stock Out", $"Device Issued Out (IMEI: {device.IMEI1}, Status: {newStatus})", executedBy);
            }

            return (true, $"Device IMEI {device.IMEI1} issued out cleanly.");
        }

        public async Task<bool> AdjustStockAsync(string productId, int quantity, string type, string reason, string executedBy)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null) return false;

            int previousStock = product.CurrentStock;
            int currentStock = previousStock;

            if (type == "Stock In")
            {
                currentStock += quantity;
            }
            else if (type == "Stock Out")
            {
                currentStock -= quantity;
                if (currentStock < 0) currentStock = 0;
            }
            else // Adjustment
            {
                currentStock += quantity;
                if (currentStock < 0) currentStock = 0;
            }

            product.CurrentStock = currentStock;
            product.UpdatedDate = DateTime.UtcNow;
            await _productRepository.UpdateAsync(product.Id, product);

            // Trigger Brevo Email Inventory Alert System
            await _inventoryAlertService.CheckAndTriggerStockAlertsAsync(product, previousStock, currentStock);

            // Look up employee details for stock attribution
            string empId = "EMP-0000";
            string empName = executedBy;
            string username = executedBy;

            if (!string.IsNullOrWhiteSpace(executedBy))
            {
                var userObj = await _userRepository.GetByUsernameAsync(executedBy);
                if (userObj != null)
                {
                    username = userObj.Username;
                    empName = userObj.FullName;
                    empId = !string.IsNullOrEmpty(userObj.EmployeeId) ? userObj.EmployeeId : $"EMP-{(userObj.Id.Length > 6 ? userObj.Id[..6] : userObj.Id)}";
                }
            }

            // Record transaction log
            var transaction = new StockTransaction
            {
                ProductId = productId,
                ProductName = product.Name,
                ProductCode = product.Code,
                EmployeeId = empId,
                EmployeeName = empName,
                Username = username,
                Quantity = global::System.Math.Abs(quantity),
                Type = type,
                Reason = reason,
                PreviousStock = previousStock,
                CurrentStock = currentStock,
                ExecutedBy = $"{empName} ({username})",
                Timestamp = DateTime.UtcNow
            };
            await _transactionRepository.CreateAsync(transaction);

            // Check Low Stock Threshold Trigger
            if (currentStock <= product.MinimumStock)
            {
                var typeAlert = currentStock == 0 ? "Danger" : "Warning";
                var title = currentStock == 0 ? "Product Out of Stock" : "Low Stock Alert";
                var message = $"Product '{product.Name}' (SKU: {product.Code}) has dropped to {currentStock} units. (Safety threshold: {product.MinimumStock})";

                await _notificationRepository.CreateAsync(new Notification
                {
                    Type = typeAlert,
                    Title = title,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                });

                try
                {
                    await _emailService.SendLowStockAlertEmailAsync("admin@sims.com", product.Name, currentStock, product.MinimumStock);
                }
                catch (Exception ex)
                {
                    global::System.Console.WriteLine($"Low stock alert email sending failed: {ex.Message}");
                }
            }

            return true;
        }

        public async Task<IEnumerable<StockTransaction>> GetProductTransactionsAsync(string productId)
        {
            return await _transactionRepository.GetTransactionsByProductIdAsync(productId);
        }

        public async Task<IEnumerable<StockTransaction>> GetRecentHistoryAsync(int count)
        {
            return await _transactionRepository.GetRecentTransactionsAsync(count);
        }

        public async Task<IEnumerable<StockTransaction>> GetPagedHistoryAsync(int page, int pageSize)
        {
            return await _transactionRepository.GetPagedTransactionsAsync(page, pageSize);
        }

        public async Task<long> GetTotalHistoryCountAsync()
        {
            return await _transactionRepository.GetTotalCountAsync();
        }

        public async Task<(IEnumerable<StockTransaction> Items, long TotalCount)> GetFilteredHistoryAsync(
            string? searchTerm,
            string? type,
            string? categoryId,
            string? productId,
            DateTime? startDate,
            DateTime? endDate,
            string? executedBy,
            int page,
            int pageSize)
        {
            List<string>? matchingProductIds = null;

            var allProducts = await _productRepository.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                matchingProductIds = allProducts.Where(p => p.CategoryId == categoryId).Select(p => p.Id).ToList();
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                var matched = allProducts.Where(p =>
                    (!string.IsNullOrEmpty(p.Name) && p.Name.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(p.Code) && p.Code.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(p.Barcode) && p.Barcode.ToLower().Contains(term))
                ).Select(p => p.Id).ToList();

                if (matchingProductIds != null)
                {
                    matchingProductIds = matchingProductIds.Intersect(matched).ToList();
                }
                else if (matched.Any())
                {
                    matchingProductIds = matched;
                }
            }

            return await _transactionRepository.GetFilteredTransactionsAsync(
                searchTerm, type, productId, matchingProductIds, startDate, endDate, executedBy, page, pageSize);
        }

        public async Task<bool> DeleteTransactionAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            await _transactionRepository.DeleteAsync(id);
            return true;
        }

        public async Task<long> DeleteTransactionsAsync(IEnumerable<string> ids)
        {
            if (ids == null || !ids.Any()) return 0;
            return await _transactionRepository.DeleteManyAsync(ids);
        }
    }
}
