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
        private readonly IPasswordResetService _passwordResetService;
        private readonly ILicenseService _licenseService;
        private readonly IAuthService _authService;

        public SupplierController(
            ISupplierService supplierService,
            IPasswordResetService passwordResetService,
            ILicenseService licenseService,
            IAuthService authService)
        {
            _supplierService = supplierService;
            _passwordResetService = passwordResetService;
            _licenseService = licenseService;
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction("Index", new { openModal = true });
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

            var tenantId = User.FindFirst("TenantId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.GroupSid)?.Value;
            var limitCheck = await _licenseService.CheckPackageLimitAsync(tenantId ?? string.Empty, "Suppliers");
            ViewBag.LimitReached = !limitCheck.Allowed;
            ViewBag.LimitMessage = limitCheck.Message;

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
        public async Task<IActionResult> Save([FromForm] Supplier supplier, [FromForm] string? confirmPassword)
        {
            var isCreating = string.IsNullOrEmpty(supplier.Id);
            var isPasswordProvided = !string.IsNullOrWhiteSpace(supplier.PasswordHash) && !supplier.PasswordHash.StartsWith("$2");

            if (isCreating)
            {
                // Check Subscription Package Supplier Limit
                var tenantId = User.FindFirst("TenantId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.GroupSid)?.Value;
                var limitCheck = await _licenseService.CheckPackageLimitAsync(tenantId ?? string.Empty, "Suppliers");
                if (!limitCheck.Allowed)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                        return Json(new { success = false, message = limitCheck.Message });

                    TempData["ToastMessage"] = limitCheck.Message;
                    TempData["ToastType"] = "danger";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(supplier.PasswordHash))
                {
                    var msg = "Portal account password is required.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                        return Json(new { success = false, message = msg });

                    TempData["ToastMessage"] = msg;
                    TempData["ToastType"] = "danger";
                    return RedirectToAction("Index");
                }

                if (supplier.PasswordHash != confirmPassword)
                {
                    var msg = "Passwords do not match.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                        return Json(new { success = false, message = msg });

                    TempData["ToastMessage"] = msg;
                    TempData["ToastType"] = "danger";
                    return RedirectToAction("Index");
                }
            }
            else if (isPasswordProvided)
            {
                if (supplier.PasswordHash != confirmPassword)
                {
                    var msg = "Passwords do not match.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                        return Json(new { success = false, message = msg });

                    TempData["ToastMessage"] = msg;
                    TempData["ToastType"] = "danger";
                    return RedirectToAction("Index");
                }
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Role.Admin)]
        public async Task<IActionResult> ChangePassword(string id, string newPassword, string confirmPassword)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message) = await _passwordResetService.DirectChangeSupplierPasswordAsync(id, newPassword, confirmPassword, executedBy);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success, message });
            }

            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Role.Admin)]
        public async Task<IActionResult> SendResetEmail(string id)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message) = await _passwordResetService.SendPasswordResetEmailForAccountAsync(id, "Supplier", executedBy, baseUrl);

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
