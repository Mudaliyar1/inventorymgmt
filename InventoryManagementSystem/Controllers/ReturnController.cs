using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Threading.Tasks;
using System.Linq;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class ReturnController : Controller
    {
        private readonly IReturnService _returnService;
        private readonly IDeviceService _deviceService;
        private readonly IProductService _productService;
        private readonly ISalesService _salesService;

        public ReturnController(
            IReturnService returnService,
            IDeviceService deviceService,
            IProductService productService,
            ISalesService salesService)
        {
            _returnService = returnService;
            _deviceService = deviceService;
            _productService = productService;
            _salesService = salesService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            int pageSize = 20;
            var returns = await _returnService.GetPagedReturnsAsync(search, page, pageSize);
            var totalCount = await _returnService.GetFilteredCountAsync(search);

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.Products = await _productService.GetAllProductsAsync();

            var (recentSales, _) = await _salesService.GetFilteredSalesAsync(null, null, null, null, null, 1, 100);
            ViewBag.RecentInvoices = recentSales;

            return View(returns);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessReturn([FromForm] ReturnRecord returnRecord)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message, result) = await _returnService.ProcessReturnAsync(returnRecord, executedBy);

            if (success)
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentInvoices()
        {
            var (sales, _) = await _salesService.GetFilteredSalesAsync(null, null, null, null, null, 1, 100);
            var list = sales.Select(s => new {
                invoiceNumber = s.InvoiceNumber,
                customerName = s.CustomerName,
                customerPhone = s.CustomerPhone,
                date = s.Date.ToString("yyyy-MM-dd HH:mm IST"),
                grandTotal = s.GrandTotal
            });
            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoiceDetails(string invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber)) return Json(new { success = false, message = "Invoice number is required" });

            var sale = await _salesService.GetSaleByInvoiceNumberAsync(invoiceNumber.Trim());
            if (sale == null) return Json(new { success = false, message = "Invoice bill not found" });

            return Json(new {
                success = true,
                invoiceNumber = sale.InvoiceNumber,
                customerName = sale.CustomerName,
                customerPhone = sale.CustomerPhone,
                items = sale.Items.Select(i => new {
                    productId = i.ProductId,
                    productName = i.ProductName,
                    imei = i.IMEI1,
                    quantity = i.Quantity,
                    sellingPrice = i.SellingPrice,
                    total = i.Total
                })
            });
        }
    }
}
