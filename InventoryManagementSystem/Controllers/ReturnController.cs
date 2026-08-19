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
        public async Task<IActionResult> Index(string? search, string? reason, string? condition, string? target, decimal? minRefund, decimal? maxRefund, int page = 1)
        {
            int pageSize = 20;
            var returns = await _returnService.GetPagedReturnsAsync(search, page, pageSize);
            var totalCount = await _returnService.GetFilteredCountAsync(search);

            ViewBag.Search = search;
            ViewBag.Reason = reason;
            ViewBag.Condition = condition;
            ViewBag.Target = target;
            ViewBag.MinRefund = minRefund;
            ViewBag.MaxRefund = maxRefund;
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

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success, message, record = result });
            }

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
                returnStatus = sale.ReturnStatus,
                totalRefundedAmount = sale.TotalRefundedAmount,
                grandTotal = sale.GrandTotal,
                netTotal = sale.NetTotal,
                items = sale.Items.Select(i => new {
                    productId = i.ProductId,
                    productName = i.ProductName,
                    imei = i.IMEI1,
                    imei1 = i.IMEI1,
                    imei2 = i.IMEI2,
                    quantity = i.Quantity,
                    sellingPrice = i.SellingPrice,
                    total = i.Total,
                    isReturned = i.IsReturned,
                    returnedQuantity = i.ReturnedQuantity
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Json(new { success = false, message = "Return ID is required." });

            var record = await _returnService.GetReturnByIdAsync(id.Trim());
            if (record == null) return Json(new { success = false, message = "Return record not found." });

            return Json(new {
                success = true,
                id = record.Id,
                returnNumber = record.ReturnNumber,
                returnDate = record.ReturnDate.ToString("yyyy-MM-dd HH:mm IST"),
                invoiceNumber = record.InvoiceNumber,
                customerName = record.CustomerName,
                customerPhone = record.CustomerPhone,
                productName = record.ProductName,
                productCode = record.ProductCode,
                imei = record.IMEI,
                quantity = record.Quantity,
                originalSellingPrice = record.OriginalSellingPrice,
                refundAmount = record.RefundAmount,
                reason = record.Reason,
                condition = record.Condition,
                deviceStatusTarget = record.DeviceStatusTarget,
                notes = record.Notes,
                executedBy = record.ExecutedBy
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReturn([FromForm] ReturnRecord returnRecord)
        {
            var updatedBy = User.Identity?.Name ?? "Admin";
            var (success, message, result) = await _returnService.UpdateReturnAsync(returnRecord, updatedBy);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success, message, record = result });
            }

            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReturn(string id)
        {
            var deletedBy = User.Identity?.Name ?? "Admin";
            var (success, message) = await _returnService.DeleteReturnAsync(id, deletedBy);

            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";
            return RedirectToAction("Index");
        }
    }
}
