using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ISalesService _salesService;
        private readonly IProductService _productService;
        private readonly IAuditLogService _auditLogService;
        private readonly Data.MongoDbContext _context;

        public SalesController(
            ISalesService salesService,
            IProductService productService,
            IAuditLogService auditLogService,
            Data.MongoDbContext context)
        {
            _salesService = salesService;
            _productService = productService;
            _auditLogService = auditLogService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? customerName,
            DateTime? startDate,
            DateTime? endDate,
            string? cashier,
            int page = 1,
            int pageSize = 15)
        {
            if (pageSize < 5) pageSize = 15;
            if (page < 1) page = 1;

            var (sales, totalItems) = await _salesService.GetFilteredSalesAsync(
                searchTerm, customerName, startDate, endDate, cashier, page, pageSize);

            int calcPages = (int)((totalItems + pageSize - 1) / pageSize);
            var totalPages = calcPages < 1 ? 1 : calcPages;

            var viewModel = new SalesListViewModel
            {
                Sales = sales,
                SearchTerm = searchTerm,
                CustomerName = customerName,
                StartDate = startDate,
                EndDate = endDate,
                Cashier = cashier,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateProductsListBag();
            var settingsColl = _context.GetCollection<Settings>("Settings");
            var settings = await settingsColl.Find(MongoDB.Driver.FilterDefinition<Settings>.Empty).FirstOrDefaultAsync();
            ViewBag.Settings = settings ?? new Settings();
            return View(new Sale { GstPercentage = settings != null ? settings.GstRate : 18.0m });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Sale sale)
        {
            decimal subTotal = 0;
            sale.CreatedBy = User.Identity?.Name ?? "System";

            var settingsColl = _context.GetCollection<Settings>("Settings");
            var settings = await settingsColl.Find(MongoDB.Driver.FilterDefinition<Settings>.Empty).FirstOrDefaultAsync();
            ViewBag.Settings = settings ?? new Settings();
            sale.CompanyGstin = settings?.GstinNumber ?? "27AAAAA0000A1Z5";

            if (sale.Items == null || !sale.Items.Any())
            {
                ModelState.AddModelError(string.Empty, "Invoice must contain at least one item.");
                await PopulateProductsListBag();
                return View(sale);
            }

            try
            {
                for (int i = 0; i < sale.Items.Count; i++)
                {
                    var item = sale.Items[i];
                    var product = await _productService.GetProductByIdAsync(item.ProductId);
                    if (product == null)
                    {
                        ModelState.AddModelError(string.Empty, $"Product not found for line item {i + 1}.");
                        await PopulateProductsListBag();
                        return View(sale);
                    }

                    if (product.CurrentStock < item.Quantity)
                    {
                        ModelState.AddModelError(string.Empty, $"Insufficient stock for '{product.Name}' (Stock: {product.CurrentStock}, Requested: {item.Quantity})");
                        await PopulateProductsListBag();
                        return View(sale);
                    }

                    item.ProductName = product.Name;
                    item.ProductCode = product.Code;
                    item.SellingPrice = product.SellingPrice;
                    item.Total = item.Quantity * product.SellingPrice;
                    subTotal += item.Total;
                }

                sale.SubTotal = subTotal;
                sale.GstAmount = (subTotal - sale.Discount) * (sale.GstPercentage / 100.0m);
                sale.GrandTotal = subTotal - sale.Discount + sale.GstAmount;

                // Calculate Amount Paid & Due Amount based on PaymentStatus
                if (sale.PaymentStatus == "Paid")
                {
                    sale.AmountPaid = sale.GrandTotal;
                    sale.DueAmount = 0m;
                }
                else if (sale.PaymentStatus == "Unpaid" || sale.PaymentStatus == "Draft")
                {
                    sale.AmountPaid = 0m;
                    sale.DueAmount = sale.GrandTotal;
                }
                else // Partial
                {
                    sale.DueAmount = sale.GrandTotal - sale.AmountPaid > 0 ? sale.GrandTotal - sale.AmountPaid : 0m;
                }

                var createdSale = await _salesService.CreateSaleAsync(sale);
                await _auditLogService.LogActivityAsync("Sale Created", User.Identity?.Name ?? "System", $"Invoice: {createdSale?.InvoiceNumber}", $"Customer: {sale.CustomerName}. Total: ₹{sale.GrandTotal:F2}, Status: {sale.PaymentStatus}");

                TempData["ToastMessage"] = $"Invoice {createdSale?.InvoiceNumber} created successfully!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateProductsListBag();
                return View(sale);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Invoice(string id)
        {
            var sale = await _salesService.GetSaleByIdAsync(id);
            if (sale == null) return NotFound();

            var settingsColl = _context.GetCollection<Settings>("Settings");
            var settings = await settingsColl.Find(MongoDB.Driver.FilterDefinition<Settings>.Empty).FirstOrDefaultAsync();
            ViewBag.Settings = settings ?? new Settings();

            return View(sale);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadInvoice(string id)
        {
            var sale = await _salesService.GetSaleByIdAsync(id);
            if (sale == null) return NotFound();

            var pdfBytes = _salesService.GenerateInvoicePdf(sale);
            return File(pdfBytes, "application/pdf", $"Invoice_{sale.InvoiceNumber}.pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var sale = await _salesService.GetSaleByIdAsync(id);
            var success = await _salesService.DeleteSaleAsync(id);
            if (success)
            {
                await _auditLogService.LogActivityAsync("Invoice Deleted", User.Identity?.Name ?? "System", $"Invoice: {sale?.InvoiceNumber ?? id}", $"Deleted invoice record for {sale?.CustomerName}");
                TempData["ToastMessage"] = "Invoice record deleted successfully!";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = "Failed to delete invoice record.";
                TempData["ToastType"] = "danger";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<string> ids)
        {
            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "No invoices selected for deletion." });
            }

            var deletedCount = await _salesService.DeleteSalesAsync(ids);
            if (deletedCount > 0)
            {
                await _auditLogService.LogActivityAsync("Bulk Invoices Deleted", User.Identity?.Name ?? "System", $"{deletedCount} invoices", $"Deleted {deletedCount} POS sales invoice records");
                return Json(new { success = true, count = deletedCount, message = $"{deletedCount} sales invoice records deleted successfully!" });
            }

            return Json(new { success = false, message = "No invoice records were deleted." });
        }

        private async Task PopulateProductsListBag()
        {
            var products = await _productService.GetAllProductsAsync();
            ViewBag.ProductsList = products.Where(p => p.Status == "Active" && p.CurrentStock > 0).ToList();
        }
    }
}
