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
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            int pageSize = 20;
            var suppliers = await _supplierService.GetPagedSuppliersAsync(search, page, pageSize);
            var totalCount = await _supplierService.GetFilteredCountAsync(search);

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View(suppliers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromForm] Supplier supplier)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message, result) = await _supplierService.SaveSupplierAsync(supplier, executedBy);

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
    }
}
