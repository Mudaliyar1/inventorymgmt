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
        private readonly ICustomerService _customerService;
        private readonly IDeviceService _deviceService;
        private readonly IAuditLogService _auditLogService;
        private readonly Data.MongoDbContext _context;

        public SalesController(
            ISalesService salesService,
            IProductService productService,
            ICustomerService customerService,
            IDeviceService deviceService,
            IAuditLogService auditLogService,
            Data.MongoDbContext context)
        {
            _salesService = salesService;
            _productService = productService;
            _customerService = customerService;
            _deviceService = deviceService;
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
            string? paymentStatus,
            string? paymentMethod,
            decimal? minAmount,
            decimal? maxAmount,
            string? sortBy,
            bool isDescending = true,
            int page = 1,
            int pageSize = 15)
        {
            if (pageSize < 5) pageSize = 15;
            if (page < 1) page = 1;

            var (sales, totalItems) = await _salesService.GetFilteredSalesAsync(
                searchTerm, customerName, startDate, endDate, cashier, page, pageSize,
                paymentStatus, paymentMethod, minAmount, maxAmount, sortBy, isDescending);

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
                PaymentStatus = paymentStatus,
                PaymentMethod = paymentMethod,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                SortBy = sortBy,
                IsDescending = isDescending,
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
            ViewBag.Customers = await _customerService.GetAllCustomersAsync();
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
                ViewBag.Customers = await _customerService.GetAllCustomersAsync();
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
                        ViewBag.Customers = await _customerService.GetAllCustomersAsync();
                        return View(sale);
                    }

                    if (product.CurrentStock < item.Quantity)
                    {
                        ModelState.AddModelError(string.Empty, $"Insufficient stock for '{product.Name}' (Stock: {product.CurrentStock}, Requested: {item.Quantity})");
                        await PopulateProductsListBag();
                        ViewBag.Customers = await _customerService.GetAllCustomersAsync();
                        return View(sale);
                    }

                    item.ProductName = product.Name;
                    item.ProductCode = product.Code;
                    item.Brand = product.Brand;
                    item.ModelName = product.ModelName;
                    item.Variant = product.Variant;
                    item.Color = product.Color;

                    if (item.SellingPrice <= 0)
                    {
                        item.SellingPrice = product.SellingPrice;
                    }
                    item.Total = item.Quantity * item.SellingPrice;
                    subTotal += item.Total;
                }

                sale.SubTotal = subTotal;
                decimal totalDiscounts = sale.Discount + sale.ExchangeDiscount;
                sale.GstAmount = (subTotal - totalDiscounts) * (sale.GstPercentage / 100.0m);
                sale.GrandTotal = System.Math.Max(0m, (subTotal - totalDiscounts) + sale.GstAmount);

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
                await _auditLogService.LogActivityAsync("Sale Created", User.Identity?.Name ?? "System", $"Invoice: {createdSale?.InvoiceNumber}", $"Customer: {sale.CustomerName}. Total: ₹{sale.GrandTotal:N2}, Status: {sale.PaymentStatus}");

                TempData["ToastMessage"] = $"Mobile Shop Invoice {createdSale?.InvoiceNumber} generated successfully!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateProductsListBag();
                ViewBag.Customers = await _customerService.GetAllCustomersAsync();
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
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var sale = await _salesService.GetSaleByIdAsync(id);
            if (sale == null) return NotFound();

            var products = await _productService.GetAllProductsAsync();
            ViewBag.AllProducts = products.Where(p => p.Status == "Active").ToList();

            return View(sale);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string id,
            string customerName,
            string customerPhone,
            string paymentStatus,
            decimal discount,
            decimal amountPaid,
            List<string> productIds,
            List<int> quantities,
            List<decimal> prices)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var existingSale = await _salesService.GetSaleByIdAsync(id);
            if (existingSale == null) return NotFound();

            if (productIds == null || productIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "An invoice must contain at least one product line item.");
                var products = await _productService.GetAllProductsAsync();
                ViewBag.AllProducts = products.Where(p => p.Status == "Active").ToList();
                return View(existingSale);
            }

            var newItems = new List<SaleItem>();
            for (int i = 0; i < productIds.Count; i++)
            {
                var pid = productIds[i];
                var qty = (i < quantities.Count && quantities[i] > 0) ? quantities[i] : 1;
                var price = (i < prices.Count && prices[i] >= 0) ? prices[i] : 0m;

                var prod = await _productService.GetProductByIdAsync(pid);
                if (prod != null)
                {
                    newItems.Add(new SaleItem
                    {
                        ProductId = prod.Id,
                        ProductName = prod.Name,
                        ProductCode = prod.Code,
                        Quantity = qty,
                        SellingPrice = price,
                        Total = qty * price
                    });
                }
            }

            try
            {
                var currentUser = User.Identity?.Name ?? "System";
                var updatedSale = await _salesService.UpdateSaleAsync(
                    id, customerName, customerPhone, paymentStatus, discount, amountPaid, newItems, currentUser);

                if (updatedSale != null)
                {
                    await _auditLogService.LogActivityAsync(
                        "Invoice Modified", currentUser, $"Invoice: {updatedSale.InvoiceNumber}",
                        $"Updated invoice details for customer {updatedSale.CustomerName}. Total: ₹{updatedSale.GrandTotal:N2}");

                    TempData["ToastMessage"] = $"Invoice {updatedSale.InvoiceNumber} updated successfully!";
                    TempData["ToastType"] = "success";
                    return RedirectToAction(nameof(Invoice), new { id = updatedSale.Id });
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }

            var allProds = await _productService.GetAllProductsAsync();
            ViewBag.AllProducts = allProds.Where(p => p.Status == "Active").ToList();
            return View(existingSale);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadInvoice(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var sale = await _salesService.GetSaleByIdAsync(id);
            if (sale == null) return NotFound();

            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

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
