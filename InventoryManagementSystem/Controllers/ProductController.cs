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
    public class ProductController : Controller
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IImageService _imageService;
        private readonly IAuditLogService _auditLogService;
        private readonly INotificationRepository _notificationRepository;
        private readonly IMobileSpecSearchService _specSearchService;
        private readonly IDeviceRepository _deviceRepository;

        public ProductController(
            IProductService productService,
            ICategoryService categoryService,
            IImageService imageService,
            IAuditLogService auditLogService,
            INotificationRepository notificationRepository,
            IMobileSpecSearchService specSearchService,
            IDeviceRepository deviceRepository)
        {
            _productService = productService;
            _categoryService = categoryService;
            _imageService = imageService;
            _auditLogService = auditLogService;
            _notificationRepository = notificationRepository;
            _specSearchService = specSearchService;
            _deviceRepository = deviceRepository;
        }



        public async Task<IActionResult> Index(
            string? search, string? categoryId, string? brand, string? modelName, 
            string? stockStatus, string? statusFilter, decimal? minPrice, decimal? maxPrice, 
            int? minStock, int? maxStock, string? productSource, string? sortBy, bool isDescending = false, int page = 1)
        {
            const int pageSize = 10;
            var products = await _productService.GetPagedProductsAsync(
                search, categoryId, sortBy, isDescending, page, pageSize, 
                brand, modelName, stockStatus, statusFilter, minPrice, maxPrice, minStock, maxStock, productSource);

            var totalItems = await _productService.GetFilteredCountAsync(
                search, categoryId, brand, modelName, stockStatus, statusFilter, minPrice, maxPrice, minStock, maxStock, productSource);

            var categories = await _categoryService.GetActiveCategoriesAsync();

            var viewModel = new ProductListViewModel
            {
                Products = products,
                Categories = categories,
                Search = search,
                SelectedCategoryId = categoryId,
                Brand = brand,
                ModelName = modelName,
                StockStatus = stockStatus,
                StatusFilter = statusFilter,
                ProductSource = productSource,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MinStock = minStock,
                MaxStock = maxStock,
                SortBy = sortBy,
                IsDescending = isDescending,
                CurrentPage = page,
                TotalItems = totalItems,
                PageSize = pageSize,
                TotalPages = (int)System.Math.Ceiling((double)totalItems / pageSize)
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            var categoryName = "N/A";
            if (!string.IsNullOrEmpty(product.CategoryId))
            {
                var category = await _categoryService.GetCategoryByIdAsync(product.CategoryId);
                if (category != null) categoryName = category.Name;
            }

            ViewBag.CategoryName = categoryName;
            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ProductCreateViewModel();
            await PopulateCategoriesList(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductCreateViewModel model)
        {
            // Validate SKU Code uniqueness
            var existingByCode = await _productService.GetProductByCodeAsync(model.Code);
            if (existingByCode != null)
            {
                ModelState.AddModelError(nameof(model.Code), "Product SKU Code is already in use.");
            }

            // Validate Barcode uniqueness
            var existingByBarcode = await _productService.GetProductByBarcodeAsync(model.Barcode);
            if (existingByBarcode != null)
            {
                ModelState.AddModelError(nameof(model.Barcode), "Barcode is already in use by another product.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCategoriesList(model);
                return View(model);
            }

            var product = new Product
            {
                Name = model.Name,
                Code = model.Code.ToUpper(),
                Barcode = model.Barcode,
                CategoryId = model.CategoryId,
                ProductType = model.ProductType ?? "Smartphone",
                Brand = model.Brand ?? string.Empty,
                ModelName = model.ModelName ?? string.Empty,
                Variant = model.Variant ?? string.Empty,
                Color = model.Color ?? string.Empty,
                PurchasePrice = model.PurchasePrice,
                SellingPrice = model.SellingPrice,
                CurrentStock = model.InitialStock,
                MinimumStock = model.MinimumStock,
                Description = model.Description,
                Status = model.Status,
                Specs = model.Specs ?? new MobileSpecifications()
            };

            // Image Upload
            if (model.ProductImage != null && model.ProductImage.Length > 0)
            {
                var uploadResult = await _imageService.UploadImageAsync(model.ProductImage, "products");
                if (uploadResult.IsSuccess)
                {
                    product.ImageUrl = uploadResult.SecureUrl;
                    product.ImagePublicId = uploadResult.PublicId;
                    product.ImageOriginalFilename = uploadResult.OriginalFilename;
                }
                else
                {
                    ModelState.AddModelError(nameof(model.ProductImage), uploadResult.ErrorMessage);
                    await PopulateCategoriesList(model);
                    return View(model);
                }
            }

            try
            {
                await _productService.CreateProductAsync(product);
                await _auditLogService.LogActivityAsync("Product Added", User.Identity?.Name ?? "System", $"Product: {product.Name}", $"Added SKU: {product.Code} with initial stock {product.CurrentStock}.");

                // Save system notification
                await _notificationRepository.CreateAsync(new Notification
                {
                    Type = "Success",
                    Title = "Product Added",
                    Message = $"Product '{product.Name}' has been added with {product.CurrentStock} units.",
                    Timestamp = DateTime.UtcNow
                });

                TempData["ToastMessage"] = "Product added successfully!";
                TempData["ToastType"] = "success";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Failed to add product: {ex.Message}");
                await PopulateCategoriesList(model);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            var model = new ProductEditViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Code = product.Code,
                Barcode = product.Barcode,
                CategoryId = product.CategoryId,
                ProductType = product.ProductType ?? "Smartphone",
                Brand = product.Brand,
                ModelName = product.ModelName,
                Variant = product.Variant,
                Color = product.Color,
                PurchasePrice = product.PurchasePrice,
                SellingPrice = product.SellingPrice,
                MinimumStock = product.MinimumStock,
                Description = product.Description,
                Status = product.Status,
                CurrentImageUrl = product.ImageUrl,
                Specs = product.Specs ?? new MobileSpecifications()
            };

            await PopulateCategoriesList(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductEditViewModel model)
        {
            Console.WriteLine($"[EDIT POST] Id={model.Id}, Name={model.Name}, Code={model.Code}, PurchasePrice={model.PurchasePrice}, SellingPrice={model.SellingPrice}");

            if (string.IsNullOrWhiteSpace(model.Id))
            {
                TempData["ToastMessage"] = "Invalid product ID. Please try again.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            var existingProduct = await _productService.GetProductByIdAsync(model.Id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            var existingByCode = await _productService.GetProductByCodeAsync(model.Code);
            if (existingByCode != null && existingByCode.Id != model.Id)
            {
                ModelState.AddModelError(nameof(model.Code), "Product SKU Code is already in use by another product.");
            }

            var existingByBarcode = await _productService.GetProductByBarcodeAsync(model.Barcode);
            if (existingByBarcode != null && existingByBarcode.Id != model.Id)
            {
                ModelState.AddModelError(nameof(model.Barcode), "Barcode is already in use by another product.");
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                Console.WriteLine($"[EDIT POST] ModelState INVALID. Errors: {errors}");
                await PopulateCategoriesList(model);
                return View(model);
            }

            Console.WriteLine($"[EDIT POST] ModelState valid. Proceeding to update product ID={model.Id}");

            // Apply only editable fields on top of the existing document to preserve stock, dates, etc.
            existingProduct.Name = model.Name;
            existingProduct.Code = model.Code.ToUpper();
            existingProduct.Barcode = model.Barcode;
            existingProduct.CategoryId = model.CategoryId;
            existingProduct.ProductType = model.ProductType ?? "Smartphone";
            existingProduct.Brand = model.Brand ?? string.Empty;
            existingProduct.ModelName = model.ModelName ?? string.Empty;
            existingProduct.Variant = model.Variant ?? string.Empty;
            existingProduct.Color = model.Color ?? string.Empty;
            existingProduct.PurchasePrice = model.PurchasePrice;
            existingProduct.SellingPrice = model.SellingPrice;
            existingProduct.MinimumStock = model.MinimumStock;
            existingProduct.Description = model.Description ?? string.Empty;
            existingProduct.Status = model.Status;
            existingProduct.Specs = model.Specs ?? new MobileSpecifications();

            if (model.ProductImage != null && model.ProductImage.Length > 0)
            {
                var uploadResult = await _imageService.UploadImageAsync(model.ProductImage, "products");
                if (uploadResult.IsSuccess)
                {
                    existingProduct.ImageUrl = uploadResult.SecureUrl;
                    existingProduct.ImagePublicId = uploadResult.PublicId;
                    existingProduct.ImageOriginalFilename = uploadResult.OriginalFilename;
                }
                else
                {
                    ModelState.AddModelError(nameof(model.ProductImage), uploadResult.ErrorMessage);
                    await PopulateCategoriesList(model);
                    return View(model);
                }
            }

            try
            {
                await _productService.UpdateProductAsync(existingProduct);
                Console.WriteLine($"[EDIT POST] UpdateProductAsync completed for ID={model.Id}");
                await _auditLogService.LogActivityAsync("Product Updated", User.Identity?.Name ?? "System", $"Product ID: {model.Id}", $"Updated product details for {model.Name}.");

                TempData["ToastMessage"] = "Product updated successfully!";
                TempData["ToastType"] = "success";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Failed to update product: {ex.Message}");
                await PopulateCategoriesList(model);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            // Cleanup image in Cloudinary
            if (!string.IsNullOrEmpty(product.ImagePublicId))
            {
                await _imageService.DeleteImageAsync(product.ImagePublicId);
            }

            // Cascade delete all physical IMEI devices linked to this Product
            await _deviceRepository.DeleteByProductIdAsync(id);

            await _productService.DeleteProductAsync(id);
            await _auditLogService.LogActivityAsync("Product Deleted", User.Identity?.Name ?? "System", $"Product: {product.Name}", $"Deleted product SKU: {product.Code} and all associated IMEI devices.");

            await _notificationRepository.CreateAsync(new Notification
            {
                Type = "Danger",
                Title = "Product Deleted",
                Message = $"Product '{product.Name}' (SKU: {product.Code}) has been deleted.",
                Timestamp = DateTime.UtcNow
            });

            TempData["ToastMessage"] = "Product deleted successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> SearchSpecsOnline(string brand, string modelName, string variant, bool allowThirdPartyFallback = false, string? customUrl = null)
        {
            var user = User.Identity?.Name ?? "Admin";
            await _auditLogService.LogActivityAsync(
                "PRODUCT_SPECIFICATION_SEARCH_STARTED",
                user,
                $"Brand: {brand}, Model: {modelName}",
                $"Search started. Target: Official Manufacturer First. FallbackAllowed: {allowThirdPartyFallback}, CustomUrl: {customUrl}");

            var result = await _specSearchService.SearchSpecificationsAsync(brand, modelName, variant, allowThirdPartyFallback, customUrl);

            if (result.Success)
            {
                await _auditLogService.LogActivityAsync(
                    "PRODUCT_SPECIFICATION_SEARCH_COMPLETED",
                    user,
                    $"Brand: {brand}, Model: {modelName}",
                    $"Specs retrieved successfully. SourceType: {result.PrimarySourceType}, ExactMatched: {result.ExactModelMatched}, Confidence: {result.ConfidenceMatch}");
            }
            else
            {
                await _auditLogService.LogActivityAsync(
                    "PRODUCT_SPECIFICATION_SEARCH_FAILED",
                    user,
                    $"Brand: {brand}, Model: {modelName}",
                    $"Search failed: {result.ErrorMessage}");
            }

            return Json(result);
        }

        public class LogSpecAppliedRequest
        {
            public string Brand { get; set; } = string.Empty;
            public string ModelName { get; set; } = string.Empty;
            public string Variant { get; set; } = string.Empty;
            public string SourceUrl { get; set; } = string.Empty;
            public string SourceType { get; set; } = "Official Manufacturer";
        }

        [HttpPost]
        public async Task<IActionResult> LogSpecsApplied([FromBody] LogSpecAppliedRequest req)
        {
            var user = User.Identity?.Name ?? "Admin";
            await _auditLogService.LogActivityAsync(
                "PRODUCT_SPECIFICATIONS_APPLIED",
                user,
                $"Brand: {req.Brand}, Model: {req.ModelName}",
                $"Admin confirmed and applied specs to form. SourceType: {req.SourceType}, SourceURL: {req.SourceUrl}");

            return Json(new { success = true });
        }

        private async Task PopulateCategoriesList(ProductCreateViewModel model)
        {
            var categories = await _categoryService.GetActiveCategoriesAsync();
            model.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.Id,
                Text = c.Name
            }).ToList();
        }

        private async Task PopulateCategoriesList(ProductEditViewModel model)
        {
            var categories = await _categoryService.GetActiveCategoriesAsync();
            model.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.Id,
                Text = c.Name
            }).ToList();
        }
    }
}

// Simple ceiling helper since Math.Ceiling returns double/decimal and needs casting
public static class Math
{
    public static int CeRounding(double value)
    {
        return (int)global::System.Math.Ceiling(value);
    }
}
