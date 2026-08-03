using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class StockController : Controller
    {
        private readonly IStockService _stockService;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IAuditLogService _auditLogService;

        public StockController(
            IStockService stockService,
            IProductService productService,
            ICategoryService categoryService,
            IAuditLogService auditLogService)
        {
            _stockService = stockService;
            _productService = productService;
            _categoryService = categoryService;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> StockIn()
        {
            var model = new StockTransactionViewModel { Type = "Stock In" };
            await PopulateProductsList(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(StockTransactionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateProductsList(model);
                return View(model);
            }

            var success = await _stockService.StockInAsync(model.ProductId, model.Quantity, model.Reason, User.Identity?.Name ?? "System");
            if (success)
            {
                var product = await _productService.GetProductByIdAsync(model.ProductId);
                await _auditLogService.LogActivityAsync("Stock In", User.Identity?.Name ?? "System", $"Product: {product?.Name}", $"Added {model.Quantity} units. Reason: {model.Reason}");

                TempData["ToastMessage"] = "Stock added successfully!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(History));
            }

            ModelState.AddModelError(string.Empty, "Error performing Stock In action.");
            await PopulateProductsList(model);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> StockOut()
        {
            var model = new StockTransactionViewModel { Type = "Stock Out" };
            await PopulateProductsList(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(StockTransactionViewModel model)
        {
            var product = await _productService.GetProductByIdAsync(model.ProductId);
            if (product != null && product.CurrentStock < model.Quantity)
            {
                ModelState.AddModelError(nameof(model.Quantity), $"Insufficient stock! Current stock: {product.CurrentStock}");
            }

            if (!ModelState.IsValid)
            {
                await PopulateProductsList(model);
                return View(model);
            }

            var success = await _stockService.StockOutAsync(model.ProductId, model.Quantity, model.Reason, User.Identity?.Name ?? "System");
            if (success)
            {
                await _auditLogService.LogActivityAsync("Stock Out", User.Identity?.Name ?? "System", $"Product: {product?.Name}", $"Removed {model.Quantity} units. Reason: {model.Reason}");

                TempData["ToastMessage"] = "Stock removed successfully!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(History));
            }

            ModelState.AddModelError(string.Empty, "Error performing Stock Out action.");
            await PopulateProductsList(model);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Adjust()
        {
            var model = new StockTransactionViewModel { Type = "Adjustment" };
            await PopulateProductsList(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(StockTransactionViewModel model, string adjustDirection)
        {
            int qty = model.Quantity;
            if (adjustDirection == "Decrease")
            {
                qty = -qty;
                var product = await _productService.GetProductByIdAsync(model.ProductId);
                if (product != null && product.CurrentStock < model.Quantity)
                {
                    ModelState.AddModelError(nameof(model.Quantity), $"Insufficient stock to decrease by that amount! Current stock: {product.CurrentStock}");
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateProductsList(model);
                return View(model);
            }

            var success = await _stockService.AdjustStockAsync(model.ProductId, qty, "Adjustment", model.Reason, User.Identity?.Name ?? "System");
            if (success)
            {
                var product = await _productService.GetProductByIdAsync(model.ProductId);
                await _auditLogService.LogActivityAsync("Stock Adjusted", User.Identity?.Name ?? "System", $"Product: {product?.Name}", $"Adjusted by {qty} units. Reason: {model.Reason}");

                TempData["ToastMessage"] = "Stock adjusted successfully!";
                TempData["ToastType"] = "info";
                return RedirectToAction(nameof(History));
            }

            ModelState.AddModelError(string.Empty, "Error performing Stock Adjustment action.");
            await PopulateProductsList(model);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> History(
            string? searchTerm,
            string? type,
            string? categoryId,
            string? productId,
            DateTime? startDate,
            DateTime? endDate,
            string? executedBy,
            int page = 1,
            int pageSize = 15)
        {
            if (pageSize < 5) pageSize = 15;
            if (page < 1) page = 1;

            var (transactions, totalItems) = await _stockService.GetFilteredHistoryAsync(
                searchTerm, type, categoryId, productId, startDate, endDate, executedBy, page, pageSize);

            var products = await _productService.GetAllProductsAsync();
            var categories = await _categoryService.GetAllCategoriesAsync();

            var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);
            var productCategories = products.ToDictionary(
                p => p.Id,
                p => !string.IsNullOrEmpty(p.CategoryId) && categoryDict.TryGetValue(p.CategoryId, out var catName) ? catName : "General"
            );

            var totalPages = (int)global::System.Math.Max(1, (totalItems + pageSize - 1) / pageSize);

            var viewModel = new StockHistoryViewModel
            {
                Transactions = transactions,
                ProductNames = products.ToDictionary(p => p.Id, p => p.Name),
                ProductCodes = products.ToDictionary(p => p.Id, p => p.Code),
                ProductCategories = productCategories,

                Categories = categories.Select(c => new SelectListItem
                {
                    Value = c.Id,
                    Text = c.Name,
                    Selected = c.Id == categoryId
                }),
                Products = products.Select(p => new SelectListItem
                {
                    Value = p.Id,
                    Text = $"{p.Name} ({p.Code})",
                    Selected = p.Id == productId
                }),
                TransactionTypes = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "All Transaction Types" },
                    new SelectListItem { Value = "Stock In", Text = "Stock In", Selected = type == "Stock In" },
                    new SelectListItem { Value = "Stock Out", Text = "Stock Out", Selected = type == "Stock Out" },
                    new SelectListItem { Value = "Adjustment", Text = "Adjustment", Selected = type == "Adjustment" }
                },

                SearchTerm = searchTerm,
                Type = type,
                CategoryId = categoryId,
                ProductId = productId,
                StartDate = startDate,
                EndDate = endDate,
                ExecutedBy = executedBy,

                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Analytics(string? productId)
        {
            var products = await _productService.GetAllProductsAsync();
            ViewBag.ProductsList = products.Select(p => new SelectListItem
            {
                Value = p.Id,
                Text = $"{p.Name} ({p.Code})"
            }).ToList();

            Product? product = null;
            var transactionsList = new List<StockTransaction>();

            if (!string.IsNullOrEmpty(productId))
            {
                product = await _productService.GetProductByIdAsync(productId);
                if (product != null)
                {
                    var txs = await _stockService.GetProductTransactionsAsync(productId);
                    transactionsList = txs.OrderBy(t => t.Timestamp).ToList();
                }
            }

            ViewBag.SelectedProduct = product;
            return View(transactionsList);
        }

        private async Task PopulateProductsList(StockTransactionViewModel model)
        {
            var products = await _productService.GetAllProductsAsync();
            model.Products = products.Where(p => p.Status == "Active").Select(p => new SelectListItem
            {
                Value = p.Id,
                Text = $"{p.Name} (SKU: {p.Code} - Qty: {p.CurrentStock})"
            }).ToList();
        }
    }
}
