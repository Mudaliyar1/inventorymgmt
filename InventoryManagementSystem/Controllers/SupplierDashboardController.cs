using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize(Roles = Role.Supplier)]
    public class SupplierDashboardController : Controller
    {
        private readonly ISupplierService _supplierService;
        private readonly ISupplierOrderService _supplierOrderService;
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAccountValidationService _accountValidationService;
        private readonly IAuditLogService _auditLogService;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IImageService _imageService;
        private readonly INotificationRepository _notificationRepository;
        private readonly IMobileSpecSearchService _specSearchService;

        public SupplierDashboardController(
            ISupplierService supplierService,
            ISupplierOrderService supplierOrderService,
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IAccountValidationService accountValidationService,
            IAuditLogService auditLogService,
            IProductService productService,
            ICategoryService categoryService,
            IImageService imageService,
            INotificationRepository notificationRepository,
            IMobileSpecSearchService specSearchService)
        {
            _supplierService = supplierService;
            _supplierOrderService = supplierOrderService;
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _accountValidationService = accountValidationService;
            _auditLogService = auditLogService;
            _productService = productService;
            _categoryService = categoryService;
            _imageService = imageService;
            _notificationRepository = notificationRepository;
            _specSearchService = specSearchService;
        }

        private string CurrentSupplierId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var supplierId = CurrentSupplierId;
            var supplier = await _supplierService.GetSupplierByIdAsync(supplierId);
            if (supplier == null) return RedirectToAction("Login", "Account");

            var allProducts = await _productRepository.GetAllAsync();
            var supplierProducts = allProducts.Where(p => p.SupplierId == supplierId).ToList();

            var orderCounts = await _supplierOrderService.GetOrderStatusCountsAsync(supplierId);
            var recentOrders = await _supplierOrderService.GetSupplierOrdersAsync(supplierId, status: null, limit: 10);

            ViewBag.Supplier = supplier;
            ViewBag.TotalProducts = supplierProducts.Count;
            ViewBag.ActiveProducts = supplierProducts.Count(p => p.Status == "Active");
            ViewBag.OrderCounts = orderCounts;

            return View(recentOrders);
        }

        [HttpGet]
        public async Task<IActionResult> Products()
        {
            var supplierId = CurrentSupplierId;
            var allProducts = await _productRepository.GetAllAsync();
            var supplierProducts = allProducts.Where(p => p.SupplierId == supplierId).OrderByDescending(p => p.CreatedDate).ToList();

            ViewBag.Categories = await _categoryRepository.GetAllAsync();
            return View(supplierProducts);
        }

        [HttpGet]
        public async Task<IActionResult> CreateProduct()
        {
            var model = new ProductCreateViewModel();
            await PopulateCategoriesList(model);
            return View("CreateProduct", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(ProductCreateViewModel model)
        {
            var supplierId = CurrentSupplierId;
            var supplier = await _supplierService.GetSupplierByIdAsync(supplierId);
            if (supplier == null) return RedirectToAction("Login", "Account");

            var existingByCode = await _productService.GetProductByCodeAsync(model.Code, supplierId);
            if (existingByCode != null)
            {
                ModelState.AddModelError(nameof(model.Code), "Product SKU Code is already in use in your catalog.");
            }

            var existingByBarcode = await _productService.GetProductByBarcodeAsync(model.Barcode, supplierId);
            if (existingByBarcode != null)
            {
                ModelState.AddModelError(nameof(model.Barcode), "Barcode is already in use in your catalog.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCategoriesList(model);
                return View("CreateProduct", model);
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
                SupplierPrice = model.PurchasePrice,
                SellingPrice = model.SellingPrice,
                CurrentStock = model.InitialStock,
                MinimumStock = model.MinimumStock,
                Description = model.Description,
                Status = model.Status,
                Specs = model.Specs ?? new MobileSpecifications(),
                SupplierId = supplierId,
                SupplierName = supplier.CompanyName,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            var imageUrls = new List<string>();

            if (model.ProductImage != null && model.ProductImage.Length > 0)
            {
                var uploadResult = await _imageService.UploadImageAsync(model.ProductImage, "products");
                if (uploadResult.IsSuccess)
                {
                    product.ImageUrl = uploadResult.SecureUrl;
                    product.ImagePublicId = uploadResult.PublicId;
                    product.ImageOriginalFilename = uploadResult.OriginalFilename;
                    imageUrls.Add(uploadResult.SecureUrl);
                }
                else
                {
                    ModelState.AddModelError(nameof(model.ProductImage), uploadResult.ErrorMessage);
                    await PopulateCategoriesList(model);
                    return View("CreateProduct", model);
                }
            }

            if (model.ProductImages != null && model.ProductImages.Any())
            {
                foreach (var file in model.ProductImages.Take(50))
                {
                    if (file == null || file.Length == 0) continue;
                    var uploadResult = await _imageService.UploadImageAsync(file, "products");
                    if (uploadResult.IsSuccess)
                    {
                        if (string.IsNullOrEmpty(product.ImageUrl))
                        {
                            product.ImageUrl = uploadResult.SecureUrl;
                            product.ImagePublicId = uploadResult.PublicId;
                            product.ImageOriginalFilename = uploadResult.OriginalFilename;
                        }
                        if (!imageUrls.Contains(uploadResult.SecureUrl)) imageUrls.Add(uploadResult.SecureUrl);
                    }
                }
            }

            product.ImageUrls = imageUrls;

            try
            {
                await _productService.CreateProductAsync(product);
                await _auditLogService.LogActivityAsync("SUPPLIER_PRODUCT_CREATED", supplier.CompanyName, product.Name, $"Added product '{product.Name}' (SKU: {product.Code}) to supplier catalog.");

                TempData["ToastMessage"] = $"Product '{product.Name}' added to your catalog successfully!";
                TempData["ToastType"] = "success";

                return RedirectToAction(nameof(Products));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Failed to add product: {ex.Message}");
                await PopulateCategoriesList(model);
                return View("CreateProduct", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var supplierId = CurrentSupplierId;
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null || product.SupplierId != supplierId) return NotFound();

            var category = !string.IsNullOrEmpty(product.CategoryId) ? await _categoryService.GetCategoryByIdAsync(product.CategoryId) : null;
            ViewBag.CategoryName = category?.Name ?? "Uncategorized";

            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> EditProduct(string id)
        {
            var supplierId = CurrentSupplierId;
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null || product.SupplierId != supplierId) return NotFound();

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
                PurchasePrice = product.SupplierPrice > 0 ? product.SupplierPrice : product.PurchasePrice,
                SellingPrice = product.SellingPrice,
                MinimumStock = product.MinimumStock,
                Description = product.Description,
                Status = product.Status,
                CurrentImageUrl = product.ImageUrl,
                ExistingImageUrls = product.ImageUrls ?? new List<string>(),
                Specs = product.Specs ?? new MobileSpecifications()
            };

            await PopulateCategoriesList(model);
            return View("EditProduct", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(ProductEditViewModel model)
        {
            var supplierId = CurrentSupplierId;
            var supplier = await _supplierService.GetSupplierByIdAsync(supplierId);

            if (string.IsNullOrWhiteSpace(model.Id)) return RedirectToAction(nameof(Products));

            var existingProduct = await _productService.GetProductByIdAsync(model.Id);
            if (existingProduct == null || existingProduct.SupplierId != supplierId)
            {
                return NotFound();
            }

            var existingByCode = await _productService.GetProductByCodeAsync(model.Code, supplierId);
            if (existingByCode != null && existingByCode.Id != model.Id)
            {
                ModelState.AddModelError(nameof(model.Code), "Product SKU Code is already in use in your catalog.");
            }

            var existingByBarcode = await _productService.GetProductByBarcodeAsync(model.Barcode, supplierId);
            if (existingByBarcode != null && existingByBarcode.Id != model.Id)
            {
                ModelState.AddModelError(nameof(model.Barcode), "Barcode is already in use in your catalog.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCategoriesList(model);
                return View("EditProduct", model);
            }

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
            existingProduct.SupplierPrice = model.PurchasePrice;
            existingProduct.SellingPrice = model.SellingPrice;
            existingProduct.MinimumStock = model.MinimumStock;
            existingProduct.Description = model.Description ?? string.Empty;
            existingProduct.Status = model.Status;
            existingProduct.Specs = model.Specs ?? new MobileSpecifications();
            existingProduct.UpdatedDate = DateTime.UtcNow;

            var imageUrls = existingProduct.ImageUrls != null ? new List<string>(existingProduct.ImageUrls) : new List<string>();

            if (model.ProductImage != null && model.ProductImage.Length > 0)
            {
                var uploadResult = await _imageService.UploadImageAsync(model.ProductImage, "products");
                if (uploadResult.IsSuccess)
                {
                    existingProduct.ImageUrl = uploadResult.SecureUrl;
                    existingProduct.ImagePublicId = uploadResult.PublicId;
                    existingProduct.ImageOriginalFilename = uploadResult.OriginalFilename;
                    if (!imageUrls.Contains(uploadResult.SecureUrl)) imageUrls.Add(uploadResult.SecureUrl);
                }
                else
                {
                    ModelState.AddModelError(nameof(model.ProductImage), uploadResult.ErrorMessage);
                    await PopulateCategoriesList(model);
                    return View("EditProduct", model);
                }
            }

            if (model.ProductImages != null && model.ProductImages.Any())
            {
                foreach (var file in model.ProductImages.Take(50))
                {
                    if (file == null || file.Length == 0) continue;
                    var uploadResult = await _imageService.UploadImageAsync(file, "products");
                    if (uploadResult.IsSuccess)
                    {
                        if (string.IsNullOrEmpty(existingProduct.ImageUrl))
                        {
                            existingProduct.ImageUrl = uploadResult.SecureUrl;
                            existingProduct.ImagePublicId = uploadResult.PublicId;
                            existingProduct.ImageOriginalFilename = uploadResult.OriginalFilename;
                        }
                        if (!imageUrls.Contains(uploadResult.SecureUrl)) imageUrls.Add(uploadResult.SecureUrl);
                    }
                }
            }

            existingProduct.ImageUrls = imageUrls;

            try
            {
                await _productService.UpdateProductAsync(existingProduct);
                await _auditLogService.LogActivityAsync("SUPPLIER_PRODUCT_UPDATED", supplier?.CompanyName ?? "Supplier", existingProduct.Name, $"Updated supplier product details for {existingProduct.Name}.");

                TempData["ToastMessage"] = "Product updated successfully!";
                TempData["ToastType"] = "success";

                return RedirectToAction(nameof(Products));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Failed to update product: {ex.Message}");
                await PopulateCategoriesList(model);
                return View("EditProduct", model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            var supplierId = CurrentSupplierId;
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null || product.SupplierId != supplierId) return NotFound();

            if (!string.IsNullOrEmpty(product.ImagePublicId))
            {
                await _imageService.DeleteImageAsync(product.ImagePublicId);
            }

            await _productService.DeleteProductAsync(id);
            await _auditLogService.LogActivityAsync("SUPPLIER_PRODUCT_DELETED", User.Identity?.Name ?? "Supplier", product.Name, $"Deleted product SKU: {product.Code}.");

            TempData["ToastMessage"] = "Product removed from catalog successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Products));
        }

        [HttpGet]
        public async Task<IActionResult> SearchSpecsOnline(string brand, string modelName, string variant, bool allowThirdPartyFallback = false, string? customUrl = null)
        {
            var user = User.Identity?.Name ?? "Supplier";
            var result = await _specSearchService.SearchSpecificationsAsync(brand, modelName, variant, allowThirdPartyFallback, customUrl);
            return Json(result);
        }

        private async Task PopulateCategoriesList(ProductCreateViewModel model)
        {
            var supplierId = CurrentSupplierId;
            var categories = await _categoryService.GetActiveCategoriesForUserAsync(supplierId);
            model.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.Id,
                Text = c.Name
            }).ToList();
        }

        private async Task PopulateCategoriesList(ProductEditViewModel model)
        {
            var supplierId = CurrentSupplierId;
            var categories = await _categoryService.GetActiveCategoriesForUserAsync(supplierId);
            model.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.Id,
                Text = c.Name
            }).ToList();
        }

        [HttpGet]
        public async Task<IActionResult> Orders(string? status, string? search, int page = 1)
        {
            var supplierId = CurrentSupplierId;
            int pageSize = 20;

            var orders = await _supplierOrderService.GetPagedOrdersAsync(search, supplierId, status, page, pageSize);
            var totalCount = await _supplierOrderService.GetFilteredCountAsync(search, supplierId, status);
            var statusCounts = await _supplierOrderService.GetOrderStatusCountsAsync(supplierId);

            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.StatusCounts = statusCounts;

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(string id)
        {
            var supplierId = CurrentSupplierId;
            var order = await _supplierOrderService.GetOrderByIdAsync(id);
            if (order == null || order.SupplierId != supplierId)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(string orderId, string newStatus, string? supplierNotes, DateTime? expectedDeliveryDate)
        {
            var supplierId = CurrentSupplierId;
            var order = await _supplierOrderService.GetOrderByIdAsync(orderId);
            if (order == null || order.SupplierId != supplierId)
            {
                TempData["ToastMessage"] = "Purchase order not found or access denied.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Orders));
            }

            // Server-side validation of allowed supplier status transitions
            if (newStatus != SupplierOrderStatus.Accepted &&
                newStatus != SupplierOrderStatus.Rejected &&
                newStatus != SupplierOrderStatus.Processing &&
                newStatus != SupplierOrderStatus.Shipped &&
                newStatus != SupplierOrderStatus.Delivered)
            {
                TempData["ToastMessage"] = "Invalid order status transition.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            var (success, message) = await _supplierOrderService.UpdateOrderStatusAsync(orderId, newStatus, User.Identity?.Name ?? "Supplier", supplierNotes, expectedDeliveryDate);
            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";

            return RedirectToAction(nameof(OrderDetails), new { id = orderId });
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var supplierId = CurrentSupplierId;
            var supplier = await _supplierService.GetSupplierByIdAsync(supplierId);
            if (supplier == null) return NotFound();

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(Supplier model, string? newPassword)
        {
            var supplierId = CurrentSupplierId;
            var existing = await _supplierService.GetSupplierByIdAsync(supplierId);
            if (existing == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.CompanyName))
            {
                ModelState.AddModelError(nameof(model.CompanyName), "Company Name is required.");
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                bool isDuplicate = await _accountValidationService.IsEmailAlreadyRegisteredAsync(model.Email, excludeSupplierId: supplierId);
                if (isDuplicate)
                {
                    ModelState.AddModelError(nameof(model.Email), "This email address is already registered with another account.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(existing);
            }

            existing.CompanyName = model.CompanyName;
            existing.ContactPerson = model.ContactPerson;
            existing.Phone = model.Phone;
            existing.Email = model.Email;
            existing.Address = model.Address;
            existing.City = model.City;
            existing.State = model.State;
            existing.Country = model.Country;
            existing.Gstin = model.Gstin;

            if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length >= 6)
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            }

            await _supplierService.SaveSupplierAsync(existing, User.Identity?.Name ?? existing.CompanyName);
            TempData["ToastMessage"] = "Your supplier portal profile was updated successfully!";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Profile));
        }
    }
}
