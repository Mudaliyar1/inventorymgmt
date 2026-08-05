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
        private readonly IStockTransactionRepository _transactionRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;

        public StockService(
            IProductRepository productRepository,
            IStockTransactionRepository transactionRepository,
            INotificationRepository notificationRepository,
            IEmailService emailService,
            IUserRepository userRepository)
        {
            _productRepository = productRepository;
            _transactionRepository = transactionRepository;
            _notificationRepository = notificationRepository;
            _emailService = emailService;
            _userRepository = userRepository;
        }

        public async Task<bool> StockInAsync(string productId, int quantity, string reason, string executedBy)
        {
            return await AdjustStockAsync(productId, quantity, "Stock In", reason, executedBy);
        }

        public async Task<bool> StockOutAsync(string productId, int quantity, string reason, string executedBy)
        {
            return await AdjustStockAsync(productId, quantity, "Stock Out", reason, executedBy);
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

            // Record transaction log with full employee attribution
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

                // System Notification
                await _notificationRepository.CreateAsync(new Notification
                {
                    Type = typeAlert,
                    Title = title,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                });

                // Email Notification
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
