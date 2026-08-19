using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class ImeiController : Controller
    {
        private readonly IDeviceService _deviceService;
        private readonly IProductService _productService;
        private readonly IStockService _stockService;
        private readonly ISalesService _salesService;

        public ImeiController(
            IDeviceService deviceService,
            IProductService productService,
            IStockService stockService,
            ISalesService salesService)
        {
            _deviceService = deviceService;
            _productService = productService;
            _stockService = stockService;
            _salesService = salesService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? productId, string? status, string? brand, int page = 1)
        {
            int pageSize = 20;
            var devices = await _deviceService.GetPagedDevicesAsync(search, productId, status, brand, page, pageSize);
            var totalCount = await _deviceService.GetFilteredCountAsync(search, productId, status, brand);

            ViewBag.Search = search;
            ViewBag.ProductId = productId;
            ViewBag.Status = status;
            ViewBag.Brand = brand;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.Products = await _productService.GetAllProductsAsync();

            return View(devices);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string imei)
        {
            if (string.IsNullOrWhiteSpace(imei))
            {
                return RedirectToAction("Index");
            }

            var device = await _deviceService.GetDeviceByImeiAsync(imei.Trim());
            if (device == null)
            {
                TempData["ToastMessage"] = $"No device record found with IMEI '{imei}'.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("Index");
            }

            Product? product = null;
            if (!string.IsNullOrEmpty(device.ProductId))
            {
                product = await _productService.GetProductByIdAsync(device.ProductId);
            }

            Sale? sale = null;
            if (!string.IsNullOrEmpty(device.InvoiceNumber))
            {
                sale = await _salesService.GetSaleByInvoiceNumberAsync(device.InvoiceNumber);
            }

            ViewBag.Product = product;
            ViewBag.Sale = sale;

            return View(device);
        }

        [HttpGet]
        public async Task<IActionResult> ValidateImei(string imei, string? excludeId)
        {
            var isAvailable = await _deviceService.ValidateImeiUniquenessAsync(imei, excludeId);
            return Json(new { available = isAvailable, message = isAvailable ? "IMEI is valid and available." : "IMEI already exists in inventory!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var deletedBy = User.Identity?.Name ?? "Admin";
            var (success, message) = await _deviceService.DeleteDeviceAsync(id, deletedBy);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success, message });
            }

            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";
            return RedirectToAction(nameof(Index));
        }
    }
}
