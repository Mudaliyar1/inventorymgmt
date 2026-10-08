using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Threading.Tasks;
using System.Security.Claims;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly ILicenseService _licenseService;

        public SubscriptionController(ILicenseService licenseService)
        {
            _licenseService = licenseService;
        }

        [HttpGet]
        public async Task<IActionResult> Plans()
        {
            var tenantId = User.FindFirst("TenantId")?.Value ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
            var currentPackage = await _licenseService.GetTenantPackageAsync(tenantId ?? string.Empty);
            var activeLicense = await _licenseService.GetActiveLicenseAsync(tenantId ?? string.Empty);
            var allPackages = await _licenseService.GetAllPackagesAsync();

            ViewBag.CurrentPackage = currentPackage;
            ViewBag.ActiveLicense = activeLicense;

            return View(allPackages);
        }
    }
}
