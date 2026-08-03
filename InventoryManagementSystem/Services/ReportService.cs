using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InventoryManagementSystem.Services
{
    public class ReportService : IReportService
    {
        public ReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateSalesExcelReport(DateTime start, DateTime end, IEnumerable<Sale> sales)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Sales Summary");

                // Style Header
                var headerRange = worksheet.Range("A1:G1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
                headerRange.Style.Font.FontColor = XLColor.White;

                // Headers
                worksheet.Cell(1, 1).Value = "Invoice Number";
                worksheet.Cell(1, 2).Value = "Customer Name";
                worksheet.Cell(1, 3).Value = "Billing Date (UTC)";
                worksheet.Cell(1, 4).Value = "Sub-Total";
                worksheet.Cell(1, 5).Value = "Discount";
                worksheet.Cell(1, 6).Value = "GST Tax";
                worksheet.Cell(1, 7).Value = "Grand Total";

                int row = 2;
                foreach (var sale in sales)
                {
                    worksheet.Cell(row, 1).Value = sale.InvoiceNumber;
                    worksheet.Cell(row, 2).Value = sale.CustomerName;
                    worksheet.Cell(row, 3).Value = sale.Date.ToString("yyyy-MM-dd HH:mm");
                    worksheet.Cell(row, 4).Value = sale.SubTotal;
                    worksheet.Cell(row, 5).Value = sale.Discount;
                    worksheet.Cell(row, 6).Value = sale.GstAmount;
                    worksheet.Cell(row, 7).Value = sale.GrandTotal;

                    // Formats
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "₹#,##0.00";
                    worksheet.Cell(row, 5).Style.NumberFormat.Format = "₹#,##0.00";
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "₹#,##0.00";
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "₹#,##0.00";

                    row++;
                }

                // Add Sum Formulas
                if (row > 2)
                {
                    var totalRow = row + 1;
                    worksheet.Cell(totalRow, 3).Value = "Total Summary";
                    worksheet.Cell(totalRow, 3).Style.Font.Bold = true;

                    worksheet.Cell(totalRow, 4).FormulaA1 = $"=SUM(D2:D{row - 1})";
                    worksheet.Cell(totalRow, 5).FormulaA1 = $"=SUM(E2:E{row - 1})";
                    worksheet.Cell(totalRow, 6).FormulaA1 = $"=SUM(F2:F{row - 1})";
                    worksheet.Cell(totalRow, 7).FormulaA1 = $"=SUM(G2:G{row - 1})";

                    worksheet.Range($"D{totalRow}:G{totalRow}").Style.Font.Bold = true;
                    worksheet.Range($"D{totalRow}:G{totalRow}").Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    worksheet.Range($"D{totalRow}:G{totalRow}").Style.NumberFormat.Format = "₹#,##0.00";
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public byte[] GenerateInventoryValuationExcelReport(IEnumerable<Product> products, Dictionary<string, string> categoryNames)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Inventory Valuation");

                // Style Header
                var headerRange = worksheet.Range("A1:H1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0f766e");
                headerRange.Style.Font.FontColor = XLColor.White;

                // Headers
                worksheet.Cell(1, 1).Value = "SKU Code";
                worksheet.Cell(1, 2).Value = "Product Name";
                worksheet.Cell(1, 3).Value = "Category";
                worksheet.Cell(1, 4).Value = "Purchase Price";
                worksheet.Cell(1, 5).Value = "Selling Price";
                worksheet.Cell(1, 6).Value = "Current Stock";
                worksheet.Cell(1, 7).Value = "Valuation (Buy)";
                worksheet.Cell(1, 8).Value = "Status";

                int row = 2;
                foreach (var prod in products)
                {
                    var catName = categoryNames.TryGetValue(prod.CategoryId, out var name) ? name : "Unclassified";
                    decimal valuation = prod.CurrentStock * prod.PurchasePrice;

                    worksheet.Cell(row, 1).Value = prod.Code;
                    worksheet.Cell(row, 2).Value = prod.Name;
                    worksheet.Cell(row, 3).Value = catName;
                    worksheet.Cell(row, 4).Value = prod.PurchasePrice;
                    worksheet.Cell(row, 5).Value = prod.SellingPrice;
                    worksheet.Cell(row, 6).Value = prod.CurrentStock;
                    worksheet.Cell(row, 7).Value = valuation;
                    worksheet.Cell(row, 8).Value = prod.Status;

                    // Formats
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "₹#,##0.00";
                    worksheet.Cell(row, 5).Style.NumberFormat.Format = "₹#,##0.00";
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "₹#,##0.00";

                    row++;
                }

                // Add Sum Formulas
                if (row > 2)
                {
                    var totalRow = row + 1;
                    worksheet.Cell(totalRow, 5).Value = "Total Value";
                    worksheet.Cell(totalRow, 5).Style.Font.Bold = true;

                    worksheet.Cell(totalRow, 6).FormulaA1 = $"=SUM(F2:F{row - 1})";
                    worksheet.Cell(totalRow, 7).FormulaA1 = $"=SUM(G2:G{row - 1})";

                    worksheet.Range($"F{totalRow}:G{totalRow}").Style.Font.Bold = true;
                    worksheet.Range($"F{totalRow}:G{totalRow}").Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(totalRow, 7).Style.NumberFormat.Format = "₹#,##0.00";
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public byte[] GenerateInventoryPdfReport(IEnumerable<Product> products, Dictionary<string, string> categoryNames)
        {
            decimal totalValuation = products.Sum(p => p.CurrentStock * p.PurchasePrice);
            long totalQuantity = products.Sum(p => p.CurrentStock);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("SMART INVENTORY MANAGEMENT SYSTEM").FontSize(16).Bold().FontColor(Colors.Teal.Darken3);
                                c.Item().Text("Corporate Inventory Valuation Summary").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });

                            row.ConstantItem(150).Column(c =>
                            {
                                c.Item().Text($"Date: {DateTime.UtcNow.ToIstString("yyyy-MM-dd HH:mm IST")}").AlignRight().FontSize(9);
                                c.Item().Text("Report Type: Live PDF").AlignRight().FontSize(9).Italic();
                            });
                        });
                        col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Teal.Darken1);
                    });

                    // Content Table
                    page.Content().PaddingTop(20).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);  // #
                                columns.ConstantColumn(80);  // SKU
                                columns.RelativeColumn();     // Name
                                columns.ConstantColumn(90);  // Category
                                columns.ConstantColumn(60);  // Price
                                columns.ConstantColumn(50);  // Qty
                                columns.ConstantColumn(80);  // Valuation
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Teal.Darken2).Padding(4).Text("#").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Teal.Darken2).Padding(4).Text("SKU").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Teal.Darken2).Padding(4).Text("Product Details").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Teal.Darken2).Padding(4).Text("Category").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Teal.Darken2).Padding(4).Text("Cost Price").AlignRight().FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Teal.Darken2).Padding(4).Text("Stock").AlignCenter().FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Teal.Darken2).Padding(4).Text("Total Cost").AlignRight().FontColor(Colors.White).Bold();
                            });

                            int index = 1;
                            foreach (var p in products)
                            {
                                var background = index % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                var catName = categoryNames.TryGetValue(p.CategoryId, out var name) ? name : "Unclassified";
                                decimal valuation = p.CurrentStock * p.PurchasePrice;

                                table.Cell().Background(background).Padding(4).Text(index.ToString());
                                table.Cell().Background(background).Padding(4).Text(p.Code);
                                table.Cell().Background(background).Padding(4).Text(p.Name);
                                table.Cell().Background(background).Padding(4).Text(catName);
                                table.Cell().Background(background).Padding(4).Text($"₹{p.PurchasePrice:F2}").AlignRight();
                                table.Cell().Background(background).Padding(4).Text(p.CurrentStock.ToString()).AlignCenter();
                                table.Cell().Background(background).Padding(4).Text($"₹{valuation:F2}").AlignRight().Bold();

                                index++;
                            }
                        });

                        // Valuation Metrics Box
                        col.Item().PaddingTop(25).Row(row =>
                        {
                            row.RelativeItem(); // Spacer
                            row.ConstantItem(250).Background(Colors.Grey.Lighten4).Padding(10).Column(metrics =>
                            {
                                metrics.Item().Text("VALUATION STATS").Bold().FontSize(10).FontColor(Colors.Teal.Darken3);
                                metrics.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                                metrics.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Total Stock Count:");
                                    r.ConstantItem(80).Text(totalQuantity.ToString()).AlignRight().Bold();
                                });
                                metrics.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Total Assets Value:");
                                    r.ConstantItem(100).Text($"₹{totalValuation:N2}").AlignRight().Bold().FontColor(Colors.Teal.Darken4);
                                });
                            });
                        });
                    });

                    // Footer
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
