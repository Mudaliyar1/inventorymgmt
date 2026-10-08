using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Linq;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly ILicenseService _licenseService;
        private readonly IFeatureCatalogService _featureCatalogService;

        public SubscriptionController(ILicenseService licenseService, IFeatureCatalogService featureCatalogService)
        {
            _licenseService = licenseService;
            _featureCatalogService = featureCatalogService;
        }

        [HttpGet]
        public async Task<IActionResult> Plans()
        {
            var tenantId = User.FindFirst("TenantId")?.Value ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
            var currentPackage = await _licenseService.GetTenantPackageAsync(tenantId ?? string.Empty);
            var activeLicense = await _licenseService.GetActiveLicenseAsync(tenantId ?? string.Empty);
            var allPackages = await _licenseService.GetPublicPackagesAsync();
            var groupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();
            var allFeatures = await _featureCatalogService.GetActiveFeaturesAsync();

            ViewBag.CurrentPackage = currentPackage;
            ViewBag.ActiveLicense = activeLicense;
            ViewBag.GroupedFeatures = groupedFeatures;
            ViewBag.AllFeatures = allFeatures;

            return View(allPackages);
        }
    }
}
