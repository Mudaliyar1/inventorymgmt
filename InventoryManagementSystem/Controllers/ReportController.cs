using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IReportService _reportService;

        public ReportController(
            ISaleRepository saleRepository,
            IProductService productService,
            ICategoryService categoryService,
            IReportService reportService)
        {
            _saleRepository = saleRepository;
            _productService = productService;
            _categoryService = categoryService;
            _reportService = reportService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            var end = endDate ?? DateTime.UtcNow;
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);

            // Get sales in range
            var sales = await _saleRepository.GetSalesBetweenDatesAsync(start, end);
            var products = await _productService.GetAllProductsAsync();

            // Aggregations
            decimal totalRevenue = sales.Sum(s => s.GrandTotal);
            int salesCount = sales.Count();
            decimal avgInvoice = salesCount > 0 ? totalRevenue / salesCount : 0.0m;

            decimal totalInventoryValuation = products.Sum(p => p.CurrentStock * p.PurchasePrice);
            int lowStockCount = products.Count(p => p.CurrentStock <= p.MinimumStock);

            ViewBag.StartDate = start.ToString("yyyy-MM-dd");
            ViewBag.EndDate = end.ToString("yyyy-MM-dd");
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.SalesCount = salesCount;
            ViewBag.AvgInvoice = avgInvoice;
            ViewBag.InventoryValuation = totalInventoryValuation;
            ViewBag.LowStockCount = lowStockCount;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExportSalesExcel(string startDate, string endDate)
        {
            if (!DateTime.TryParse(startDate, out var start)) start = DateTime.UtcNow.AddDays(-30);
            if (!DateTime.TryParse(endDate, out var end)) end = DateTime.UtcNow;

            var sales = await _saleRepository.GetSalesBetweenDatesAsync(start, end);
            var fileBytes = _reportService.GenerateSalesExcelReport(start, end, sales);

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Sales_Report_{start:yyyyMMdd}_to_{end:yyyyMMdd}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportInventoryExcel()
        {
            var products = await _productService.GetAllProductsAsync();
            var categories = await _categoryService.GetAllCategoriesAsync();
            var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);

            var fileBytes = _reportService.GenerateInventoryValuationExcelReport(products, categoryDict);

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Inventory_Valuation_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportInventoryPdf()
        {
            var products = await _productService.GetAllProductsAsync();
            var categories = await _categoryService.GetAllCategoriesAsync();
            var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);

            var fileBytes = _reportService.GenerateInventoryPdfReport(products, categoryDict);

            return File(fileBytes, "application/pdf", $"Inventory_Valuation_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
    }
}
