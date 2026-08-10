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

        public ProductController(
            IProductService productService,
            ICategoryService categoryService,
            IImageService imageService,
            IAuditLogService auditLogService,
            INotificationRepository notificationRepository)
        {
            _productService = productService;
            _categoryService = categoryService;
            _imageService = imageService;
            _auditLogService = auditLogService;
            _notificationRepository = notificationRepository;
        }

        public async Task<IActionResult> Index(string? search, string? categoryId, string? sortBy, bool isDescending = false, int page = 1)
        {
            const int pageSize = 10;
            var products = await _productService.GetPagedProductsAsync(search, categoryId, sortBy, isDescending, page, pageSize);
            var totalItems = await _productService.GetFilteredCountAsync(search, categoryId);
            var categories = await _categoryService.GetActiveCategoriesAsync();

            var viewModel = new ProductListViewModel
            {
                Products = products,
                Categories = categories,
                Search = search,
                SelectedCategoryId = categoryId,
                SortBy = sortBy,
                IsDescending = isDescending,
                CurrentPage = page,
                TotalItems = totalItems,
                PageSize = pageSize,
                TotalPages = (int)Math.CeRounding((double)totalItems / pageSize)
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
                    PurchasePrice = model.PurchasePrice,
                    SellingPrice = model.SellingPrice,
                    CurrentStock = model.InitialStock,
                    MinimumStock = model.MinimumStock,
                    Description = model.Description,
                    Status = model.Status
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
                PurchasePrice = product.PurchasePrice,
                SellingPrice = product.SellingPrice,
                MinimumStock = product.MinimumStock,
                Description = product.Description,
                Status = product.Status,
                CurrentImageUrl = product.ImageUrl
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
            existingProduct.PurchasePrice = model.PurchasePrice;
            existingProduct.SellingPrice = model.SellingPrice;
            existingProduct.MinimumStock = model.MinimumStock;
            existingProduct.Description = model.Description ?? string.Empty;
            existingProduct.Status = model.Status;

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

            await _productService.DeleteProductAsync(id);
            await _auditLogService.LogActivityAsync("Product Deleted", User.Identity?.Name ?? "System", $"Product: {product.Name}", $"Deleted product SKU: {product.Code}.");

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
