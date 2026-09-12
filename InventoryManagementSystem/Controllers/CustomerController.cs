using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly ISalesService _salesService;
        private readonly IDeviceService _deviceService;

        public CustomerController(
            ICustomerService customerService,
            ISalesService salesService,
            IDeviceService deviceService)
        {
            _customerService = customerService;
            _salesService = salesService;
            _deviceService = deviceService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? hasGstin, decimal? minPurchases, decimal? maxPurchases, int page = 1)
        {
            int pageSize = 20;
            var customers = await _customerService.GetPagedCustomersAsync(search, hasGstin, minPurchases, maxPurchases, page, pageSize);
            var totalCount = await _customerService.GetFilteredCountAsync(search, hasGstin, minPurchases, maxPurchases);

            ViewBag.Search = search;
            ViewBag.HasGstin = hasGstin;
            ViewBag.MinPurchases = minPurchases;
            ViewBag.MaxPurchases = maxPurchases;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View(customers);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(id);
            if (customer == null) return NotFound();

            var allSales = await _salesService.GetAllSalesAsync();
            var customerSales = allSales.Where(s =>
                (!string.IsNullOrEmpty(s.CustomerId) && s.CustomerId == customer.Id) ||
                (!string.IsNullOrEmpty(s.CustomerPhone) && s.CustomerPhone.Trim() == customer.Phone.Trim() && 
                 string.Equals(s.CustomerName?.Trim(), customer.Name?.Trim(), System.StringComparison.OrdinalIgnoreCase))
            ).OrderByDescending(s => s.Date).ToList();

            var devices = await _deviceService.GetPagedDevicesAsync(customer.Phone, null, null, null, 1, 100);

            ViewBag.Sales = customerSales;
            ViewBag.Devices = devices;

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateStats()
        {
            var success = await _customerService.RecalculateAllCustomerStatsAsync();
            TempData["ToastMessage"] = success ? "Customer directory purchase metrics synced successfully from invoices." : "Failed to recalculate metrics.";
            TempData["ToastType"] = success ? "success" : "danger";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromForm] Customer customer)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message, result) = await _customerService.SaveCustomerAsync(customer, executedBy);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success, message, customer = result });
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message) = await _customerService.DeleteCustomerAsync(id, executedBy);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success, message });
            }

            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";

            return RedirectToAction("Index");
        }
    }
}
