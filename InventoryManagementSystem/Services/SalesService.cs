using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Extensions;
using InventoryManagementSystem.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class SalesService : ISalesService
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly IStockService _stockService;
        private readonly MongoDbContext _context;

        public SalesService(
            ISaleRepository saleRepository,
            IProductRepository productRepository,
            IStockService stockService,
            MongoDbContext context)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _stockService = stockService;
            _context = context;
            
            // Set QuestPDF license type
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<Sale?> CreateSaleAsync(Sale sale)
        {
            // Validate and deduct stock for each item before saving
            foreach (var item in sale.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null || product.CurrentStock < item.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for product '{item.ProductName}' (Available: {product?.CurrentStock ?? 0})");
                }
            }

            sale.InvoiceNumber = await GenerateInvoiceNumberAsync();
            sale.Date = DateTime.UtcNow;

            // Save Sale record
            await _saleRepository.CreateAsync(sale);

            // Deduct stock and log stock transaction for each item
            foreach (var item in sale.Items)
            {
                await _stockService.StockOutAsync(item.ProductId, item.Quantity, $"Invoice Sale: {sale.InvoiceNumber}", sale.CreatedBy);
            }

            return sale;
        }

        public async Task<string> GenerateInvoiceNumberAsync()
        {
            var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var random = new Random();
            var suffix = random.Next(1000, 9999).ToString();
            var invoiceNumber = $"INV-{dateStr}-{suffix}";

            // Ensure invoice number uniqueness
            var existing = await _saleRepository.GetByInvoiceNumberAsync(invoiceNumber);
            if (existing != null)
            {
                return await GenerateInvoiceNumberAsync(); // Regenerate on collision
            }

            return invoiceNumber;
        }

        public async Task<Sale?> GetSaleByIdAsync(string id)
        {
            return await _saleRepository.GetByIdAsync(id);
        }

        public async Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber)
        {
            return await _saleRepository.GetByInvoiceNumberAsync(invoiceNumber);
        }

        public async Task<IEnumerable<Sale>> GetPagedSalesAsync(int page, int pageSize)
        {
            return await _saleRepository.GetPagedSalesAsync(page, pageSize);
        }

        public async Task<long> GetTotalSalesCountAsync()
        {
            return await _saleRepository.GetTotalSalesCountAsync();
        }

        public async Task<(IEnumerable<Sale> Items, long TotalCount)> GetFilteredSalesAsync(
            string? searchTerm,
            string? customerName,
            DateTime? startDate,
            DateTime? endDate,
            string? cashier,
            int page,
            int pageSize)
        {
            return await _saleRepository.GetFilteredSalesAsync(searchTerm, customerName, startDate, endDate, cashier, page, pageSize);
        }

        public async Task<Sale?> UpdateSaleAsync(
            string saleId,
            string customerName,
            string customerPhone,
            string paymentStatus,
            decimal discount,
            decimal amountPaid,
            List<SaleItem> newItems,
            string updatedBy)
        {
            var existingSale = await _saleRepository.GetByIdAsync(saleId);
            if (existingSale == null) return null;

            // 1. Revert previous stock deductions
            foreach (var item in existingSale.Items)
            {
                await _stockService.StockInAsync(item.ProductId, item.Quantity, $"Invoice #{existingSale.InvoiceNumber} Edit Reversal", updatedBy);
            }

            // 2. Validate and apply new stock deductions
            foreach (var item in newItems)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null || product.CurrentStock < item.Quantity)
                {
                    // Re-apply original stock before throwing exception
                    foreach (var orig in existingSale.Items)
                    {
                        await _stockService.StockOutAsync(orig.ProductId, orig.Quantity, $"Invoice #{existingSale.InvoiceNumber} Edit Rollback", updatedBy);
                    }
                    throw new InvalidOperationException($"Insufficient stock for product '{item.ProductName}' (Available: {product?.CurrentStock ?? 0})");
                }
            }

            foreach (var item in newItems)
            {
                await _stockService.StockOutAsync(item.ProductId, item.Quantity, $"Invoice #{existingSale.InvoiceNumber} Updated Sale", updatedBy);
            }

            // 3. Recalculate totals
            decimal subTotal = newItems.Sum(i => i.Quantity * i.SellingPrice);
            decimal gstAmount = System.Math.Round((subTotal - discount) * (existingSale.GstPercentage / 100m), 2);
            decimal grandTotal = System.Math.Max(0m, (subTotal - discount) + gstAmount);
            decimal dueAmount = System.Math.Max(0m, grandTotal - amountPaid);

            existingSale.CustomerName = customerName ?? string.Empty;
            existingSale.CustomerPhone = customerPhone ?? string.Empty;
            existingSale.PaymentStatus = paymentStatus ?? "Paid";
            existingSale.Discount = discount;
            existingSale.SubTotal = subTotal;
            existingSale.GstAmount = gstAmount;
            existingSale.GrandTotal = grandTotal;
            existingSale.AmountPaid = amountPaid;
            existingSale.DueAmount = dueAmount;
            existingSale.Items = newItems;

            await _saleRepository.UpdateAsync(existingSale.Id, existingSale);
            return existingSale;
        }

        public async Task<bool> DeleteSaleAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            var sale = await _saleRepository.GetByIdAsync(id);
            if (sale == null) return false;

            // Restock products when deleting an invoice
            foreach (var item in sale.Items)
            {
                await _stockService.StockInAsync(item.ProductId, item.Quantity, $"Invoice #{sale.InvoiceNumber} Cancellation/Deletion", sale.CreatedBy);
            }

            await _saleRepository.DeleteAsync(id);
            return true;
        }

        public async Task<long> DeleteSalesAsync(IEnumerable<string> ids)
        {
            if (ids == null || !ids.Any()) return 0;
            long count = 0;
            foreach (var id in ids)
            {
                if (await DeleteSaleAsync(id))
                {
                    count++;
                }
            }
            return count;
        }

        public byte[] GenerateInvoicePdf(Sale sale)
        {
            var settings = _context.GetCollection<Settings>("Settings").Find(FilterDefinition<Settings>.Empty).FirstOrDefault() ?? new Settings();

            var companyName = !string.IsNullOrWhiteSpace(settings.CompanyName) ? settings.CompanyName : "SMART INVENTORY MANAGEMENT SYSTEM";
            var companyGstin = !string.IsNullOrWhiteSpace(sale.CompanyGstin) ? sale.CompanyGstin : (!string.IsNullOrWhiteSpace(settings.GstinNumber) ? settings.GstinNumber : "27AAAAA0000A1Z5");
            var companyPhone = !string.IsNullOrWhiteSpace(settings.CompanyPhone) ? settings.CompanyPhone : "+91 98765 43210";
            var companyEmail = !string.IsNullOrWhiteSpace(settings.CompanyEmail) ? settings.CompanyEmail : "support@sims.com";
            var companyAddress = !string.IsNullOrWhiteSpace(settings.Address) ? settings.Address : "123 Business Hub, Mumbai, India";
            var formattedDate = sale.Date.ToIstString("yyyy-MM-dd HH:mm IST");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // Header Section
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(companyName.ToUpper()).FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                            col.Item().Text(companyAddress).FontSize(9).FontColor(Colors.Grey.Darken1);
                            col.Item().Text($"Phone: {companyPhone} · Email: {companyEmail}").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                            col.Item().Text($"Tax GSTIN: {companyGstin}").FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                        });

                        row.ConstantItem(200).Column(col =>
                        {
                            col.Item().Text("TAX INVOICE").FontSize(20).Bold().AlignRight().FontColor(Colors.Blue.Darken4);
                            col.Item().Text($"Invoice #: {sale.InvoiceNumber}").AlignRight().Bold().FontSize(11);
                            col.Item().Text($"Date: {formattedDate}").AlignRight().FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                    });

                    // Customer Details
                    page.Content().Column(col =>
                    {
                        col.Item().PaddingTop(15).PaddingBottom(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Billed To:").Bold().FontColor(Colors.Grey.Darken3);
                                c.Item().Text(sale.CustomerName).FontSize(11).Bold();
                                if (!string.IsNullOrWhiteSpace(sale.CustomerPhone))
                                {
                                    c.Item().Text($"Phone: {sale.CustomerPhone}").FontSize(9.5f);
                                }
                            });

                            row.ConstantItem(200).Column(c =>
                            {
                                c.Item().Text("Payment Status:").Bold().FontColor(Colors.Grey.Darken3);
                                var statusColor = sale.PaymentStatus == "Paid" ? Colors.Green.Darken2 : (sale.PaymentStatus == "Partial" ? Colors.Orange.Darken2 : Colors.Red.Darken2);
                                c.Item().Text((sale.PaymentStatus ?? "Paid").ToUpper()).FontSize(11).Bold().FontColor(statusColor);
                                c.Item().Text($"Cashier: {sale.CreatedBy}").FontSize(9.5f);
                            });
                        });

                        // Main Items Table
                        col.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(35);  // S.No
                                columns.RelativeColumn();     // Item Name
                                columns.ConstantColumn(90);  // SKU
                                columns.ConstantColumn(80);  // Price
                                columns.ConstantColumn(50);  // Qty
                                columns.ConstantColumn(90);  // Total
                            });

                            // Table Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("#").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Item Details").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("SKU").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Unit Price").AlignRight().FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Qty").AlignCenter().FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Total").AlignRight().FontColor(Colors.White).Bold();
                            });

                            // Table Rows
                            int index = 1;
                            foreach (var item in sale.Items)
                            {
                                var background = index % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Background(background).Padding(5).Text(index.ToString());
                                table.Cell().Background(background).Padding(5).Text(item.ProductName);
                                table.Cell().Background(background).Padding(5).Text(item.ProductCode);
                                table.Cell().Background(background).Padding(5).Text($"₹{item.SellingPrice:F2}").AlignRight();
                                table.Cell().Background(background).Padding(5).Text(item.Quantity.ToString()).AlignCenter();
                                table.Cell().Background(background).Padding(5).Text($"₹{item.Total:F2}").AlignRight().Bold();

                                index++;
                            }
                        });

                        // Calculations Block
                        col.Item().AlignRight().PaddingTop(15).Row(row =>
                        {
                            row.ConstantItem(260).Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn();
                                    cols.ConstantColumn(110);
                                });

                                summaryTable.Cell().Padding(3).Text("Sub-Total:").AlignRight();
                                summaryTable.Cell().Padding(3).Text($"₹{sale.SubTotal:F2}").AlignRight();

                                summaryTable.Cell().Padding(3).Text($"GST ({sale.GstPercentage}%):").AlignRight();
                                summaryTable.Cell().Padding(3).Text($"₹{sale.GstAmount:F2}").AlignRight();

                                if (sale.Discount > 0)
                                {
                                    summaryTable.Cell().Padding(3).Text("Discount Applied:").AlignRight().FontColor(Colors.Red.Medium);
                                    summaryTable.Cell().Padding(3).Text($"-₹{sale.Discount:F2}").AlignRight().FontColor(Colors.Red.Medium);
                                }

                                summaryTable.Cell().BorderTop(1).BorderColor(Colors.Grey.Lighten1).Padding(4).Text("Grand Total:").AlignRight().Bold();
                                summaryTable.Cell().BorderTop(1).BorderColor(Colors.Grey.Lighten1).Padding(4).Text($"₹{sale.GrandTotal:F2}").AlignRight().Bold().FontSize(11.5f).FontColor(Colors.Blue.Darken3);

                                summaryTable.Cell().Padding(3).Text("Amount Paid:").AlignRight().FontColor(Colors.Green.Darken2);
                                summaryTable.Cell().Padding(3).Text($"₹{sale.AmountPaid:F2}").AlignRight().Bold().FontColor(Colors.Green.Darken2);

                                if (sale.DueAmount > 0)
                                {
                                    summaryTable.Cell().Padding(3).Text("Due Amount:").AlignRight().FontColor(Colors.Red.Medium);
                                    summaryTable.Cell().Padding(3).Text($"₹{sale.DueAmount:F2}").AlignRight().Bold().FontColor(Colors.Red.Medium);
                                }
                            });
                        });

                        // Thank You Message
                        col.Item().PaddingTop(30).AlignCenter().Text("Thank you for your business!").FontSize(10.5f).Italic().FontColor(Colors.Grey.Darken1);
                        col.Item().AlignCenter().Text($"For support or inquiries, please contact: {companyEmail}").FontSize(8).FontColor(Colors.Grey.Medium);
                    });

                    // Page Footer
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf();
        }
    }
}
