using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class SupplierController : Controller
    {
        private readonly ISupplierService _supplierService;

        public SupplierController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? terms, string? payableStatus, int page = 1)
        {
            int pageSize = 20;
            var suppliers = await _supplierService.GetPagedSuppliersAsync(search, terms, payableStatus, page, pageSize);
            var totalCount = await _supplierService.GetFilteredCountAsync(search, terms, payableStatus);

            ViewBag.Search = search;
            ViewBag.Terms = terms;
            ViewBag.PayableStatus = payableStatus;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View(suppliers);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Json(new { success = false, message = "Invalid Supplier ID." });
            var supplier = await _supplierService.GetSupplierByIdAsync(id);
            if (supplier == null) return Json(new { success = false, message = "Supplier not found." });
            return Json(new { success = true, supplier });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromForm] Supplier supplier)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message, result) = await _supplierService.SaveSupplierAsync(supplier, executedBy);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success, message, supplier = result });
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
            var (success, message) = await _supplierService.DeleteSupplierAsync(id, executedBy);
            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";
            return RedirectToAction("Index");
        }
    }
}
