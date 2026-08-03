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
    public class SalesController : Controller
    {
        private readonly ISalesService _salesService;
        private readonly IProductService _productService;
        private readonly IAuditLogService _auditLogService;

        public SalesController(
            ISalesService salesService,
            IProductService productService,
            IAuditLogService auditLogService)
        {
            _salesService = salesService;
            _productService = productService;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 15;
            var sales = await _salesService.GetPagedSalesAsync(page, pageSize);
            var totalItems = await _salesService.GetTotalSalesCountAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.CeRounding((double)totalItems / pageSize);

            return View(sales);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateProductsListBag();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Sale sale)
        {
            decimal subTotal = 0;
            sale.CreatedBy = User.Identity?.Name ?? "System";

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

                var createdSale = await _salesService.CreateSaleAsync(sale);
                await _auditLogService.LogActivityAsync("Sale Created", User.Identity?.Name ?? "System", $"Invoice: {createdSale?.InvoiceNumber}", $"Customer: {sale.CustomerName}. Total: ₹{sale.GrandTotal:F2}");

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

        private async Task PopulateProductsListBag()
        {
            var products = await _productService.GetAllProductsAsync();
            ViewBag.ProductsList = products.Where(p => p.Status == "Active" && p.CurrentStock > 0).ToList();
        }
    }
}
