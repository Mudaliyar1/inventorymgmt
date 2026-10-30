using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;

namespace InventoryManagementSystem.Services
{
    public class ReceiptService : IReceiptService
    {
        public ReceiptService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string GenerateReceiptNumber()
        {
            // Format: RCP-YYYYMMDD-XXXX
            var istNow = DateTime.UtcNow.AddMinutes(330);
            var randomSuffix = Random.Shared.Next(1000, 9999);
            return $"RCP-{istNow:yyyyMMdd}-{randomSuffix}";
        }

        public byte[] GeneratePaymentReceiptPdf(
            SaaSPaymentTransaction transaction, 
            SubscriptionPackage? package, 
            PlatformSettings? platformSettings)
        {
            var companyName = !string.IsNullOrWhiteSpace(platformSettings?.PlatformName) 
                ? platformSettings.PlatformName 
                : "Smart Inventory Management System (SIMS)";
            var companyEmail = !string.IsNullOrWhiteSpace(platformSettings?.ContactEmail) 
                ? platformSettings.ContactEmail 
                : "support@sims.com";
            var companyPhone = !string.IsNullOrWhiteSpace(platformSettings?.ContactPhone) 
                ? platformSettings.ContactPhone 
                : "+91 98765 43210";
            var companyAddress = !string.IsNullOrWhiteSpace(platformSettings?.OfficeAddress) 
                ? platformSettings.OfficeAddress 
                : "SIMS SaaS Platforms India Pvt Ltd, Technology Park, Bengaluru, India";

            var receiptNo = !string.IsNullOrWhiteSpace(transaction.ReceiptNumber) 
                ? transaction.ReceiptNumber 
                : GenerateReceiptNumber();

            var issueDateUtc = transaction.ReceiptIssuedAt ?? transaction.VerifiedDate ?? transaction.CreatedDate;
            var issueDateIst = issueDateUtc.AddMinutes(330).ToString("dd MMM yyyy, hh:mm tt") + " IST";

            var packageName = !string.IsNullOrWhiteSpace(transaction.PackageName) 
                ? transaction.PackageName 
                : (package?.Name ?? "SIMS Subscription Package");

            var purposeFormatted = transaction.PaymentPurpose switch
            {
                "NewPurchase" => "New Subscription Purchase",
                "Renewal" => "Subscription License Renewal",
                "Upgrade" => "Subscription Plan Upgrade",
                _ => transaction.PaymentPurpose
            };

            var customerDisplayName = !string.IsNullOrWhiteSpace(transaction.CustomerName) 
                ? transaction.CustomerName 
                : (!string.IsNullOrWhiteSpace(transaction.ShopName) ? transaction.ShopName : "Valued Customer");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Arial"));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(brandCol =>
                            {
                                brandCol.Item().Text(companyName).FontSize(16).Bold().FontColor("#0284c7");
                                brandCol.Item().Text(companyAddress).FontSize(8.5f).FontColor("#64748b");
                                brandCol.Item().Text($"Support: {companyEmail} | Tel: {companyPhone}").FontSize(8.5f).FontColor("#64748b");
                            });

                            row.ConstantItem(220).Column(receiptBadgeCol =>
                            {
                                receiptBadgeCol.Item().Text("PAYMENT RECEIPT").FontSize(18).Bold().AlignRight().FontColor("#0f172a");
                                receiptBadgeCol.Item().Text($"Receipt No: {receiptNo}").AlignRight().Bold().FontSize(10).FontColor("#334155");
                                receiptBadgeCol.Item().Text($"Issued: {issueDateIst}").AlignRight().FontSize(8.5f).FontColor("#64748b");
                                receiptBadgeCol.Item().Text("STATUS: VERIFIED & PAID").AlignRight().Bold().FontSize(8.5f).FontColor("#16a34a");
                            });
                        });

                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor("#e2e8f0");
                    });

                    // Body
                    page.Content().Column(col =>
                    {
                        // Customer / Business Information Box
                        col.Item().Background("#f8fafc").Padding(12).Border(1).BorderColor("#e2e8f0").Row(infoRow =>
                        {
                            infoRow.RelativeItem().Column(custCol =>
                            {
                                custCol.Item().Text("RECEIVED FROM:").FontSize(8).Bold().FontColor("#64748b");
                                custCol.Item().Text(customerDisplayName).FontSize(11).Bold().FontColor("#0f172a");
                                if (!string.IsNullOrWhiteSpace(transaction.ShopName) && transaction.ShopName != customerDisplayName)
                                {
                                    custCol.Item().Text($"Shop / Business: {transaction.ShopName}").FontSize(9).FontColor("#334155");
                                }
                                if (!string.IsNullOrWhiteSpace(transaction.CustomerEmail))
                                {
                                    custCol.Item().Text($"Email: {transaction.CustomerEmail}").FontSize(9).FontColor("#334155");
                                }
                                if (!string.IsNullOrWhiteSpace(transaction.CustomerPhone))
                                {
                                    custCol.Item().Text($"Phone: {transaction.CustomerPhone}").FontSize(9).FontColor("#334155");
                                }
                            });

                            infoRow.RelativeItem().Column(metaCol =>
                            {
                                metaCol.Item().Text("TRANSACTION DETAILS:").FontSize(8).Bold().FontColor("#64748b");
                                metaCol.Item().Text($"Internal Ref: {transaction.TransactionRef}").FontSize(9).Bold().FontColor("#0f172a");
                                metaCol.Item().Text($"Razorpay Payment ID: {(!string.IsNullOrEmpty(transaction.RazorpayPaymentId) ? transaction.RazorpayPaymentId : "N/A")}").FontSize(9).FontColor("#334155");
                                metaCol.Item().Text($"Razorpay Order ID: {(!string.IsNullOrEmpty(transaction.RazorpayOrderId) ? transaction.RazorpayOrderId : "N/A")}").FontSize(9).FontColor("#334155");
                                metaCol.Item().Text($"Payment Purpose: {purposeFormatted}").FontSize(9).FontColor("#334155");
                            });
                        });

                        col.Item().PaddingTop(20);

                        // Line Item Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#0f172a").Padding(6).Text("#").Bold().FontColor("#ffffff").FontSize(8.5f);
                                header.Cell().Background("#0f172a").Padding(6).Text("DESCRIPTION / PACKAGE").Bold().FontColor("#ffffff").FontSize(8.5f);
                                header.Cell().Background("#0f172a").Padding(6).Text("BILLING CYCLE").Bold().FontColor("#ffffff").FontSize(8.5f);
                                header.Cell().Background("#0f172a").Padding(6).Text("DURATION").Bold().FontColor("#ffffff").FontSize(8.5f);
                                header.Cell().Background("#0f172a").Padding(6).AlignRight().Text("AMOUNT (INR)").Bold().FontColor("#ffffff").FontSize(8.5f);
                            });

                            table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(8).Text("1").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(8).Column(descCol =>
                            {
                                descCol.Item().Text(packageName).Bold().FontSize(9.5f).FontColor("#0f172a");
                                descCol.Item().Text(purposeFormatted).FontSize(8).FontColor("#64748b");
                            });
                            table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(8).Text(transaction.BillingCycle).FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(8).Text($"{transaction.DurationMonths} Month(s)").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(8).AlignRight().Text($"₹ {transaction.Amount:N2}").Bold().FontSize(9.5f);
                        });

                        col.Item().PaddingTop(15);

                        // Summary & Totals
                        col.Item().Row(totRow =>
                        {
                            totRow.RelativeItem(2).Column(noteCol =>
                            {
                                noteCol.Item().Background("#f1f5f9").Padding(10).Column(c =>
                                {
                                    c.Item().Text("ACKNOWLEDGEMENT").FontSize(8).Bold().FontColor("#475569");
                                    c.Item().Text("Thank you for your business. We have successfully received and verified your payment. Your subscription is active.").FontSize(8.5f).FontColor("#334155");
                                    c.Item().PaddingTop(4).Text("This document serves as an official confirmation and receipt of payment for software services.").FontSize(7.5f).Italic().FontColor("#64748b");
                                });
                            });

                            totRow.ConstantItem(20);

                            totRow.RelativeItem(2).Column(sumCol =>
                            {
                                sumCol.Item().BorderBottom(1).BorderColor("#cbd5e1").PaddingVertical(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Subtotal:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"₹ {transaction.Amount:N2}").FontSize(9);
                                });

                                sumCol.Item().BorderBottom(1).BorderColor("#cbd5e1").PaddingVertical(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Taxes (GST 18% incl.):").FontSize(9);
                                    r.RelativeItem().AlignRight().Text("Included").FontSize(8.5f).FontColor("#64748b");
                                });

                                sumCol.Item().Background("#e0f2fe").Padding(6).Row(r =>
                                {
                                    r.RelativeItem().Text("TOTAL PAID:").Bold().FontSize(11).FontColor("#0369a1");
                                    r.RelativeItem().AlignRight().Text($"₹ {transaction.Amount:N2}").Bold().FontSize(11).FontColor("#0369a1");
                                });
                            });
                        });
                    });

                    // Footer
                    page.Footer().Column(fCol =>
                    {
                        fCol.Item().LineHorizontal(1).LineColor("#e2e8f0");
                        fCol.Item().PaddingTop(6).Row(fRow =>
                        {
                            fRow.RelativeItem().Text("Smart Inventory Management System (SIMS) — Cloud SaaS Edition").FontSize(8).FontColor("#94a3b8");
                            fRow.RelativeItem().AlignRight().Text($"Printed: {DateTime.UtcNow.AddMinutes(330):yyyy-MM-dd HH:mm} IST").FontSize(8).FontColor("#94a3b8");
                        });
                    });
                });
            }).GeneratePdf();
        }
    }
}
