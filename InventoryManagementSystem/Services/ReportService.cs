using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.ViewModels;
using InventoryManagementSystem.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class ReportService : IReportService
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IStockTransactionRepository _stockTransactionRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IUserRepository _userRepository;

        public ReportService(
            ISaleRepository saleRepository,
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IStockTransactionRepository stockTransactionRepository,
            IAuditLogRepository auditLogRepository,
            IUserRepository userRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _stockTransactionRepository = stockTransactionRepository;
            _auditLogRepository = auditLogRepository;
            _userRepository = userRepository;

            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<ReportResultData> BuildReportDataAsync(ReportFilterRequest request)
        {
            int pSize = request.PageSize < 5 ? 20 : (request.PageSize > 200 ? 200 : request.PageSize);
            int pNum = request.Page < 1 ? 1 : request.Page;

            var result = new ReportResultData
            {
                ReportType = request.ReportType,
                Page = pNum,
                PageSize = pSize,
                GeneratedAt = DateTime.UtcNow
            };

            // 1. Normalize Date Range
            var (startUtc, endUtc, dateLabel) = ResolveDateRange(request.DatePreset, request.StartDate, request.EndDate);

            // Fetch lookup metadata
            var categories = (await _categoryRepository.GetAllAsync()).ToList();
            var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);
            var products = (await _productRepository.GetAllAsync()).ToList();
            var productDict = products.ToDictionary(p => p.Id, p => p);

            // Filter helper string
            var filterParts = new List<string> { $"Date: {dateLabel}" };
            if (!string.IsNullOrWhiteSpace(request.SearchTerm)) filterParts.Add($"Search: '{request.SearchTerm}'");
            if (!string.IsNullOrWhiteSpace(request.CategoryId) && categoryDict.TryGetValue(request.CategoryId, out var catName)) filterParts.Add($"Category: {catName}");
            if (!string.IsNullOrWhiteSpace(request.ProductId) && productDict.TryGetValue(request.ProductId, out var prodItem)) filterParts.Add($"Product: {prodItem.Name}");
            if (!string.IsNullOrWhiteSpace(request.TransactionType)) filterParts.Add($"Type: {request.TransactionType}");
            if (!string.IsNullOrWhiteSpace(request.PaymentStatus)) filterParts.Add($"Payment: {request.PaymentStatus}");
            if (!string.IsNullOrWhiteSpace(request.StockStatus) && request.StockStatus != "All") filterParts.Add($"Stock: {request.StockStatus}");
            result.AppliedFiltersText = string.Join(" | ", filterParts);

            // Global stats base calculation
            var globalStats = new ReportSummaryStats
            {
                TotalProductsCount = products.Count,
                TotalCategoriesCount = categories.Count,
                CurrentInventoryValue = products.Sum(p => p.CurrentStock * p.PurchasePrice),
                PotentialSalesValue = products.Sum(p => p.CurrentStock * p.SellingPrice),
                TotalInventoryQty = products.Sum(p => p.CurrentStock),
                LowStockCount = products.Count(p => p.CurrentStock > 0 && p.CurrentStock <= p.MinimumStock),
                OutOfStockCount = products.Count(p => p.CurrentStock == 0),
                TotalProfitPotential = products.Sum(p => p.CurrentStock * (p.SellingPrice - p.PurchasePrice))
            };

            // Switch by Report Type
            switch ((request.ReportType ?? "Sales").Trim().ToLower())
            {
                case "inventory":
                    BuildInventoryReport(request, result, products, categoryDict, globalStats);
                    break;

                case "stockmovement":
                case "stockhistory":
                    await BuildStockMovementReportAsync(request, result, startUtc, endUtc, products, categoryDict, globalStats);
                    break;

                case "employeeactivity":
                    await BuildEmployeeActivityReportAsync(request, result, startUtc, endUtc, globalStats);
                    break;

                case "alerts":
                    BuildAlertsReport(request, result, products, categoryDict, globalStats);
                    break;

                case "category":
                    await BuildCategoryReportAsync(request, result, startUtc, endUtc, products, categories, globalStats);
                    break;

                case "productperformance":
                    await BuildProductPerformanceReportAsync(request, result, startUtc, endUtc, products, categoryDict, globalStats);
                    break;

                case "executivesummary":
                    await BuildExecutiveSummaryReportAsync(request, result, startUtc, endUtc, products, categories, globalStats);
                    break;

                case "sales":
                default:
                    await BuildSalesReportAsync(request, result, startUtc, endUtc, products, categoryDict, globalStats);
                    break;
            }

            return result;
        }

        #region Report Builders

        private async Task BuildSalesReportAsync(ReportFilterRequest req, ReportResultData res, DateTime start, DateTime end, List<Product> products, Dictionary<string, string> catDict, ReportSummaryStats stats)
        {
            res.ReportTitle = "Detailed Sales & Commercial Revenue Report";
            res.Headers = new List<string> { "#", "Invoice #", "Date & Time (IST)", "Customer", "Items Purchased", "Subtotal", "Discount", "GST Tax", "Grand Total", "Payment Status", "Cashier / User" };

            var (sales, totalCount) = await _saleRepository.GetFilteredSalesAsync(
                req.SearchTerm, customerName: null, start, end, cashier: req.EmployeeId, 1, 50000);

            var salesList = sales.ToList();

            // Additional filters
            if (!string.IsNullOrWhiteSpace(req.PaymentStatus))
            {
                salesList = salesList.Where(s => string.Equals(s.PaymentStatus, req.PaymentStatus, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(req.ProductId))
            {
                salesList = salesList.Where(s => s.Items.Any(i => i.ProductId == req.ProductId)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(req.CategoryId))
            {
                var catProdIds = products.Where(p => p.CategoryId == req.CategoryId).Select(p => p.Id).ToHashSet();
                salesList = salesList.Where(s => s.Items.Any(i => catProdIds.Contains(i.ProductId))).ToList();
            }

            // Calculate exact metrics for selected set
            stats.TotalSalesRevenue = salesList.Sum(s => s.GrandTotal);
            stats.TotalOrders = salesList.Count;
            stats.TotalItemsSold = salesList.Sum(s => s.Items.Sum(i => i.Quantity));
            stats.TotalDiscounts = salesList.Sum(s => s.Discount);
            stats.TotalGstTax = salesList.Sum(s => s.GstAmount);
            stats.AverageOrderValue = stats.TotalOrders > 0 ? stats.TotalSalesRevenue / stats.TotalOrders : 0.0m;
            res.SummaryStats = stats;

            res.TotalCount = salesList.Count;
            int pages = (int)System.Math.Ceiling((double)res.TotalCount / res.PageSize);
            res.TotalPages = pages < 1 ? 1 : pages;

            var paged = salesList.Skip((res.Page - 1) * res.PageSize).Take(res.PageSize);

            foreach (var s in paged)
            {
                var itemsSummary = string.Join(", ", s.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));
                res.Rows.Add(new ReportRowItem
                {
                    Id = s.Id,
                    PrimaryText = s.InvoiceNumber,
                    DateString = s.Date.ToIstString("yyyy-MM-dd HH:mm IST"),
                    CustomerInfo = string.IsNullOrWhiteSpace(s.CustomerName) ? "Walk-in Customer" : s.CustomerName,
                    SecondaryText = itemsSummary,
                    SubTotal = s.SubTotal,
                    Discount = s.Discount,
                    TaxAmount = s.GstAmount,
                    GrandTotal = s.GrandTotal,
                    BadgeText = s.PaymentStatus,
                    BadgeClass = string.Equals(s.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ? "bg-success" : "bg-warning text-dark",
                    UserExecutor = string.IsNullOrWhiteSpace(s.CreatedBy) ? "System" : s.CreatedBy
                });
            }
        }

        private void BuildInventoryReport(ReportFilterRequest req, ReportResultData res, List<Product> products, Dictionary<string, string> catDict, ReportSummaryStats stats)
        {
            res.ReportTitle = "Complete Inventory Stock Valuation Statement";
            res.Headers = new List<string> { "#", "SKU Code", "Product Name", "Category", "Current Stock", "Min Stock", "Stock Status", "Purchase Cost", "Selling Price", "Cost Valuation", "Sales Value", "Profit Potential" };

            var filtered = products.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(req.SearchTerm))
            {
                var term = req.SearchTerm.Trim().ToLower();
                filtered = filtered.Where(p => p.Name.ToLower().Contains(term) || p.Code.ToLower().Contains(term) || p.Barcode.ToLower().Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(req.CategoryId))
            {
                filtered = filtered.Where(p => p.CategoryId == req.CategoryId);
            }
            if (!string.IsNullOrWhiteSpace(req.ProductId))
            {
                filtered = filtered.Where(p => p.Id == req.ProductId);
            }
            if (!string.IsNullOrWhiteSpace(req.StockStatus) && req.StockStatus != "All")
            {
                if (req.StockStatus == "OutOfStock") filtered = filtered.Where(p => p.CurrentStock == 0);
                else if (req.StockStatus == "LowStock") filtered = filtered.Where(p => p.CurrentStock > 0 && p.CurrentStock <= p.MinimumStock);
                else if (req.StockStatus == "InStock") filtered = filtered.Where(p => p.CurrentStock > p.MinimumStock);
            }

            var list = filtered.OrderBy(p => p.Name).ToList();

            stats.TotalProductsCount = list.Count;
            stats.TotalInventoryQty = list.Sum(p => p.CurrentStock);
            stats.CurrentInventoryValue = list.Sum(p => p.CurrentStock * p.PurchasePrice);
            stats.PotentialSalesValue = list.Sum(p => p.CurrentStock * p.SellingPrice);
            stats.TotalProfitPotential = list.Sum(p => p.CurrentStock * (p.SellingPrice - p.PurchasePrice));
            stats.LowStockCount = list.Count(p => p.CurrentStock > 0 && p.CurrentStock <= p.MinimumStock);
            stats.OutOfStockCount = list.Count(p => p.CurrentStock == 0);
            res.SummaryStats = stats;

            res.TotalCount = list.Count;
            int pages = (int)System.Math.Ceiling((double)res.TotalCount / res.PageSize);
            res.TotalPages = pages < 1 ? 1 : pages;

            var paged = list.Skip((res.Page - 1) * res.PageSize).Take(res.PageSize);

            foreach (var p in paged)
            {
                var catName = catDict.TryGetValue(p.CategoryId, out var name) ? name : "Unclassified";
                string stStatus = p.CurrentStock == 0 ? "Out of Stock" : (p.CurrentStock <= p.MinimumStock ? "Low Stock" : "In Stock");
                string stBadge = p.CurrentStock == 0 ? "bg-danger" : (p.CurrentStock <= p.MinimumStock ? "bg-warning text-dark" : "bg-success");

                res.Rows.Add(new ReportRowItem
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    CategoryName = catName,
                    Quantity = p.CurrentStock,
                    PreviousStock = p.MinimumStock,
                    BadgeText = stStatus,
                    BadgeClass = stBadge,
                    UnitPrice = p.PurchasePrice,
                    SalesValue = p.SellingPrice,
                    CostValue = p.CurrentStock * p.PurchasePrice,
                    GrandTotal = p.CurrentStock * p.SellingPrice,
                    ProfitValue = p.CurrentStock * (p.SellingPrice - p.PurchasePrice)
                });
            }
        }

        private async Task BuildStockMovementReportAsync(ReportFilterRequest req, ReportResultData res, DateTime start, DateTime end, List<Product> products, Dictionary<string, string> catDict, ReportSummaryStats stats)
        {
            res.ReportTitle = "Stock Movement & Audit History Log Report";
            res.Headers = new List<string> { "#", "Timestamp (IST)", "Product Name", "SKU", "Movement Type", "Quantity", "Prev Stock", "New Stock", "Reason", "Executed By" };

            var (txs, count) = await _stockTransactionRepository.GetFilteredTransactionsAsync(
                req.SearchTerm, req.TransactionType, req.ProductId, matchingProductIds: null, start, end, executedBy: req.EmployeeId, 1, 50000);

            var txList = txs.ToList();

            if (!string.IsNullOrWhiteSpace(req.CategoryId))
            {
                var catProdIds = products.Where(p => p.CategoryId == req.CategoryId).Select(p => p.Id).ToHashSet();
                txList = txList.Where(t => catProdIds.Contains(t.ProductId)).ToList();
            }

            stats.TotalStockInQty = txList.Where(t => (t.Type ?? "").ToLower().Contains("in") || t.Quantity > 0).Sum(t => System.Math.Abs(t.Quantity));
            stats.TotalStockOutQty = txList.Where(t => (t.Type ?? "").ToLower().Contains("out") || (t.Type ?? "").ToLower().Contains("sale") || t.Quantity < 0).Sum(t => System.Math.Abs(t.Quantity));
            res.SummaryStats = stats;

            res.TotalCount = txList.Count;
            int pages = (int)System.Math.Ceiling((double)res.TotalCount / res.PageSize);
            res.TotalPages = pages < 1 ? 1 : pages;

            var paged = txList.Skip((res.Page - 1) * res.PageSize).Take(res.PageSize);

            foreach (var t in paged)
            {
                string bClass = (t.Type ?? "").ToLower().Contains("in") ? "bg-success" : ((t.Type ?? "").ToLower().Contains("out") || (t.Type ?? "").ToLower().Contains("sale") ? "bg-warning text-dark" : "bg-info text-dark");

                res.Rows.Add(new ReportRowItem
                {
                    Id = t.Id,
                    DateString = t.Timestamp.ToIstString("yyyy-MM-dd HH:mm IST"),
                    Name = t.ProductName,
                    Code = t.ProductCode,
                    BadgeText = t.Type ?? string.Empty,
                    BadgeClass = bClass,
                    Quantity = t.Quantity,
                    PreviousStock = t.PreviousStock,
                    NewStock = t.CurrentStock,
                    Reason = string.IsNullOrWhiteSpace(t.Reason) ? "Standard Log" : t.Reason,
                    UserExecutor = string.IsNullOrWhiteSpace(t.ExecutedBy) ? (string.IsNullOrWhiteSpace(t.Username) ? "System" : t.Username) : t.ExecutedBy
                });
            }
        }

        private async Task BuildEmployeeActivityReportAsync(ReportFilterRequest req, ReportResultData res, DateTime start, DateTime end, ReportSummaryStats stats)
        {
            res.ReportTitle = "Employee & System Audit Activity Report";
            res.Headers = new List<string> { "#", "Timestamp (IST)", "Employee / User", "Role", "Module", "Action", "Description / Target", "IP Address", "Status" };

            var (logs, count) = await _auditLogRepository.GetFilteredLogsAsync(
                req.SearchTerm, module: null, action: req.TransactionType, status: null, logLevel: null, employee: req.EmployeeId, start, end, ipAddress: null, browser: null, device: null, 1, 50000);

            var logList = logs.ToList();
            res.SummaryStats = stats;

            res.TotalCount = logList.Count;
            int pages = (int)System.Math.Ceiling((double)res.TotalCount / res.PageSize);
            res.TotalPages = pages < 1 ? 1 : pages;

            var paged = logList.Skip((res.Page - 1) * res.PageSize).Take(res.PageSize);

            foreach (var l in paged)
            {
                var empName = !string.IsNullOrWhiteSpace(l.EmployeeName) ? l.EmployeeName : (!string.IsNullOrWhiteSpace(l.ExecutedBy) ? l.ExecutedBy : l.Username);
                res.Rows.Add(new ReportRowItem
                {
                    Id = l.Id,
                    DateString = l.Timestamp.ToIstString("yyyy-MM-dd HH:mm IST"),
                    UserExecutor = empName,
                    SecondaryText = l.UserRole,
                    CategoryName = l.Module,
                    Name = l.Action,
                    PrimaryText = l.Target,
                    Code = l.IpAddress,
                    BadgeText = l.Status,
                    BadgeClass = l.Status == "Success" ? "bg-success" : "bg-danger"
                });
            }
        }

        private void BuildAlertsReport(ReportFilterRequest req, ReportResultData res, List<Product> products, Dictionary<string, string> catDict, ReportSummaryStats stats)
        {
            res.ReportTitle = "Low Stock & Out of Stock Exception Report";
            res.Headers = new List<string> { "#", "SKU Code", "Product Name", "Category", "Current Stock", "Min Threshold", "Alert Status", "Purchase Cost", "Selling Price", "Stock Shortage Qty" };

            var alerts = products.Where(p => p.CurrentStock <= p.MinimumStock).OrderBy(p => p.CurrentStock).ToList();

            if (!string.IsNullOrWhiteSpace(req.CategoryId))
            {
                alerts = alerts.Where(p => p.CategoryId == req.CategoryId).ToList();
            }

            stats.LowStockCount = alerts.Count(p => p.CurrentStock > 0);
            stats.OutOfStockCount = alerts.Count(p => p.CurrentStock == 0);
            res.SummaryStats = stats;

            res.TotalCount = alerts.Count;
            int pages = (int)System.Math.Ceiling((double)res.TotalCount / res.PageSize);
            res.TotalPages = pages < 1 ? 1 : pages;

            var paged = alerts.Skip((res.Page - 1) * res.PageSize).Take(res.PageSize);

            foreach (var p in paged)
            {
                var catName = catDict.TryGetValue(p.CategoryId, out var name) ? name : "Unclassified";
                string stStatus = p.CurrentStock == 0 ? "CRITICAL OUT OF STOCK" : "LOW STOCK WARNING";
                string stBadge = p.CurrentStock == 0 ? "bg-danger" : "bg-warning text-dark";

                res.Rows.Add(new ReportRowItem
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    CategoryName = catName,
                    Quantity = p.CurrentStock,
                    PreviousStock = p.MinimumStock,
                    BadgeText = stStatus,
                    BadgeClass = stBadge,
                    UnitPrice = p.PurchasePrice,
                    SalesValue = p.SellingPrice,
                    NewStock = p.MinimumStock > p.CurrentStock ? p.MinimumStock - p.CurrentStock : 0
                });
            }
        }

        private async Task BuildCategoryReportAsync(ReportFilterRequest req, ReportResultData res, DateTime start, DateTime end, List<Product> products, List<Category> categories, ReportSummaryStats stats)
        {
            res.ReportTitle = "Category Distribution & Revenue Summary Report";
            res.Headers = new List<string> { "#", "Category Name", "Total Products", "Total Units", "Cost Valuation", "Sales Value", "Revenue Generated", "Low Stock Products", "Out of Stock" };

            var sales = await _saleRepository.GetSalesBetweenDatesAsync(start, end);
            var salesItems = sales.SelectMany(s => s.Items).ToList();

            var categoryRows = new List<ReportRowItem>();

            foreach (var cat in categories)
            {
                var catProds = products.Where(p => p.CategoryId == cat.Id).ToList();
                var catProdIds = catProds.Select(p => p.Id).ToHashSet();

                var revenue = salesItems.Where(i => catProdIds.Contains(i.ProductId)).Sum(i => i.Total);
                var costVal = catProds.Sum(p => p.CurrentStock * p.PurchasePrice);
                var salesVal = catProds.Sum(p => p.CurrentStock * p.SellingPrice);

                categoryRows.Add(new ReportRowItem
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    Quantity = catProds.Count,
                    NewStock = catProds.Sum(p => p.CurrentStock),
                    CostValue = costVal,
                    SalesValue = salesVal,
                    GrandTotal = revenue,
                    PreviousStock = catProds.Count(p => p.CurrentStock > 0 && p.CurrentStock <= p.MinimumStock),
                    Discount = catProds.Count(p => p.CurrentStock == 0)
                });
            }

            res.SummaryStats = stats;
            res.TotalCount = categoryRows.Count;
            int pages = (int)System.Math.Ceiling((double)res.TotalCount / res.PageSize);
            res.TotalPages = pages < 1 ? 1 : pages;

            res.Rows = categoryRows.Skip((res.Page - 1) * res.PageSize).Take(res.PageSize).ToList();
        }

        private async Task BuildProductPerformanceReportAsync(ReportFilterRequest req, ReportResultData res, DateTime start, DateTime end, List<Product> products, Dictionary<string, string> catDict, ReportSummaryStats stats)
        {
            res.ReportTitle = "Product Commercial Performance & Margin Analysis";
            res.Headers = new List<string> { "#", "SKU Code", "Product Name", "Category", "Units Sold", "Revenue (₹)", "Discounts", "Current Stock", "Inventory Cost", "Gross Profit Potential" };

            var sales = await _saleRepository.GetSalesBetweenDatesAsync(start, end);
            var salesList = sales.ToList();

            var perfMap = products.Select(p => new
            {
                Product = p,
                CategoryName = catDict.TryGetValue(p.CategoryId, out var name) ? name : "Unclassified",
                UnitsSold = salesList.SelectMany(s => s.Items).Where(i => i.ProductId == p.Id).Sum(i => i.Quantity),
                Revenue = salesList.SelectMany(s => s.Items).Where(i => i.ProductId == p.Id).Sum(i => i.Total),
                CostValuation = p.CurrentStock * p.PurchasePrice,
                ProfitPotential = p.CurrentStock * (p.SellingPrice - p.PurchasePrice)
            }).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(req.CategoryId))
            {
                perfMap = perfMap.Where(x => x.Product.CategoryId == req.CategoryId);
            }
            if (!string.IsNullOrWhiteSpace(req.SearchTerm))
            {
                var term = req.SearchTerm.ToLower();
                perfMap = perfMap.Where(x => x.Product.Name.ToLower().Contains(term) || x.Product.Code.ToLower().Contains(term));
            }

            // Sorting options
            perfMap = (req.SortBy?.ToLower()) switch
            {
                "highestsales" => perfMap.OrderByDescending(x => x.UnitsSold),
                "lowestsales" => perfMap.OrderBy(x => x.UnitsSold),
                "highestrevenue" => perfMap.OrderByDescending(x => x.Revenue),
                "lowestrevenue" => perfMap.OrderBy(x => x.Revenue),
                "higheststock" => perfMap.OrderByDescending(x => x.Product.CurrentStock),
                "loweststock" => perfMap.OrderBy(x => x.Product.CurrentStock),
                _ => perfMap.OrderByDescending(x => x.Revenue)
            };

            var perfList = perfMap.ToList();
            res.SummaryStats = stats;
            res.TotalCount = perfList.Count;
            int pages = (int)System.Math.Ceiling((double)res.TotalCount / res.PageSize);
            res.TotalPages = pages < 1 ? 1 : pages;

            var paged = perfList.Skip((res.Page - 1) * res.PageSize).Take(res.PageSize);

            foreach (var item in paged)
            {
                res.Rows.Add(new ReportRowItem
                {
                    Id = item.Product.Id,
                    Code = item.Product.Code,
                    Name = item.Product.Name,
                    CategoryName = item.CategoryName,
                    Quantity = item.UnitsSold,
                    GrandTotal = item.Revenue,
                    PreviousStock = item.Product.CurrentStock,
                    CostValue = item.CostValuation,
                    ProfitValue = item.ProfitPotential
                });
            }
        }

        private async Task BuildExecutiveSummaryReportAsync(ReportFilterRequest req, ReportResultData res, DateTime start, DateTime end, List<Product> products, List<Category> categories, ReportSummaryStats stats)
        {
            await BuildSalesReportAsync(req, res, start, end, products, categories.ToDictionary(c => c.Id, c => c.Name), stats);
            res.ReportTitle = "Executive Management Performance Summary";
        }

        #endregion

        #region Helper Methods

        private (DateTime Start, DateTime End, string Label) ResolveDateRange(string preset, DateTime? customStart, DateTime? customEnd)
        {
            var nowUtc = DateTime.UtcNow;
            var todayIstDate = nowUtc.AddHours(5.5).Date;

            switch ((preset ?? "ThisMonth").Trim().ToLower())
            {
                case "today":
                    var startToday = todayIstDate.AddHours(-5.5);
                    var endToday = startToday.AddDays(1).AddTicks(-1);
                    return (startToday, endToday, "Today");

                case "yesterday":
                    var startYest = todayIstDate.AddDays(-1).AddHours(-5.5);
                    var endYest = startYest.AddDays(1).AddTicks(-1);
                    return (startYest, endYest, "Yesterday");

                case "thisweek":
                    int diff = (int)todayIstDate.DayOfWeek - (int)DayOfWeek.Monday;
                    if (diff < 0) diff += 7;
                    var startWeek = todayIstDate.AddDays(-diff).AddHours(-5.5);
                    return (startWeek, nowUtc, "This Week");

                case "lastmonth":
                    var firstThisMonth = new DateTime(todayIstDate.Year, todayIstDate.Month, 1);
                    var firstLastMonth = firstThisMonth.AddMonths(-1);
                    var lastLastMonth = firstThisMonth.AddTicks(-1);
                    return (firstLastMonth.AddHours(-5.5), lastLastMonth.AddHours(-5.5), "Last Month");

                case "thisyear":
                    var startYear = new DateTime(todayIstDate.Year, 1, 1).AddHours(-5.5);
                    return (startYear, nowUtc, "This Year");

                case "alltime":
                    return (DateTime.MinValue, DateTime.MaxValue, "All Time");

                case "custom":
                    if (customStart.HasValue && customEnd.HasValue)
                    {
                        return (customStart.Value.ToUniversalTime(), customEnd.Value.ToUniversalTime(), $"{customStart.Value:yyyy-MM-dd} to {customEnd.Value:yyyy-MM-dd}");
                    }
                    goto case "thismonth";

                case "thismonth":
                default:
                    var startMonth = new DateTime(todayIstDate.Year, todayIstDate.Month, 1).AddHours(-5.5);
                    return (startMonth, nowUtc, "This Month");
            }
        }

        #endregion

        #region Export Generation (Excel & PDF)

        public byte[] GenerateExcelReport(ReportResultData data)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add(data.ReportType);

                // Title Banner
                worksheet.Cell(1, 1).Value = "SMART INVENTORY MANAGEMENT SYSTEM";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 14;
                worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1e3a8a");

                worksheet.Cell(2, 1).Value = data.ReportTitle;
                worksheet.Cell(2, 1).Style.Font.Bold = true;
                worksheet.Cell(2, 1).Style.Font.FontSize = 12;

                worksheet.Cell(3, 1).Value = $"Generated IST: {data.GeneratedAt.ToIstString("yyyy-MM-dd HH:mm IST")} | Filters: {data.AppliedFiltersText}";
                worksheet.Cell(3, 1).Style.Font.Italic = true;
                worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;

                // Executive Metrics Table Box (Rows 5 to 7)
                worksheet.Cell(5, 1).Value = "Total Revenue";
                worksheet.Cell(5, 2).Value = data.SummaryStats.TotalSalesRevenue;
                worksheet.Cell(5, 2).Style.NumberFormat.Format = "₹#,##0.00";

                worksheet.Cell(5, 4).Value = "Asset Valuation";
                worksheet.Cell(5, 5).Value = data.SummaryStats.CurrentInventoryValue;
                worksheet.Cell(5, 5).Style.NumberFormat.Format = "₹#,##0.00";

                worksheet.Cell(6, 1).Value = "Total Orders";
                worksheet.Cell(6, 2).Value = data.SummaryStats.TotalOrders;

                worksheet.Cell(6, 4).Value = "Low Stock SKUs";
                worksheet.Cell(6, 5).Value = data.SummaryStats.LowStockCount;

                worksheet.Range("A5:E6").Style.Font.Bold = true;
                worksheet.Range("A5:E6").Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");

                // Headers at Row 9
                int startRow = 9;
                for (int col = 0; col < data.Headers.Count; col++)
                {
                    var cell = worksheet.Cell(startRow, col + 1);
                    cell.Value = data.Headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // Data Rows
                int r = startRow + 1;
                int rowNum = 1;
                foreach (var item in data.Rows)
                {
                    worksheet.Cell(r, 1).Value = rowNum++;

                    if (data.ReportType.Equals("Sales", StringComparison.OrdinalIgnoreCase))
                    {
                        worksheet.Cell(r, 2).Value = item.PrimaryText;
                        worksheet.Cell(r, 3).Value = item.DateString;
                        worksheet.Cell(r, 4).Value = item.CustomerInfo;
                        worksheet.Cell(r, 5).Value = item.SecondaryText;
                        worksheet.Cell(r, 6).Value = item.SubTotal;
                        worksheet.Cell(r, 7).Value = item.Discount;
                        worksheet.Cell(r, 8).Value = item.TaxAmount;
                        worksheet.Cell(r, 9).Value = item.GrandTotal;
                        worksheet.Cell(r, 10).Value = item.BadgeText;
                        worksheet.Cell(r, 11).Value = item.UserExecutor;

                        worksheet.Cell(r, 6).Style.NumberFormat.Format = "₹#,##0.00";
                        worksheet.Cell(r, 7).Style.NumberFormat.Format = "₹#,##0.00";
                        worksheet.Cell(r, 8).Style.NumberFormat.Format = "₹#,##0.00";
                        worksheet.Cell(r, 9).Style.NumberFormat.Format = "₹#,##0.00";
                    }
                    else if (data.ReportType.Equals("StockMovement", StringComparison.OrdinalIgnoreCase) || data.ReportType.Equals("StockHistory", StringComparison.OrdinalIgnoreCase))
                    {
                        worksheet.Cell(r, 2).Value = item.DateString;
                        worksheet.Cell(r, 3).Value = item.Name;
                        worksheet.Cell(r, 4).Value = item.Code;
                        worksheet.Cell(r, 5).Value = item.BadgeText;
                        worksheet.Cell(r, 6).Value = item.Quantity;
                        worksheet.Cell(r, 7).Value = item.PreviousStock;
                        worksheet.Cell(r, 8).Value = item.NewStock;
                        worksheet.Cell(r, 9).Value = item.Reason;
                        worksheet.Cell(r, 10).Value = item.UserExecutor;
                    }
                    else
                    {
                        worksheet.Cell(r, 2).Value = item.Code;
                        worksheet.Cell(r, 3).Value = item.Name;
                        worksheet.Cell(r, 4).Value = item.CategoryName;
                        worksheet.Cell(r, 5).Value = item.Quantity;
                        worksheet.Cell(r, 6).Value = item.PreviousStock;
                        worksheet.Cell(r, 7).Value = item.BadgeText;
                        worksheet.Cell(r, 8).Value = item.UnitPrice;
                        worksheet.Cell(r, 9).Value = item.SalesValue;
                        worksheet.Cell(r, 10).Value = item.CostValue;

                        worksheet.Cell(r, 8).Style.NumberFormat.Format = "₹#,##0.00";
                        worksheet.Cell(r, 9).Style.NumberFormat.Format = "₹#,##0.00";
                        worksheet.Cell(r, 10).Style.NumberFormat.Format = "₹#,##0.00";
                    }

                    if (r % 2 == 0)
                    {
                        worksheet.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
                    }
                    r++;
                }

                worksheet.Columns().AdjustToContents();
                worksheet.SheetView.FreezeRows(startRow);

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public byte[] GeneratePdfReport(ReportResultData data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(35);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Arial"));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("SMART INVENTORY MANAGEMENT SYSTEM").FontSize(15).Bold().FontColor(Colors.Blue.Darken4);
                                c.Item().Text(data.ReportTitle).FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                                c.Item().Text($"Filters: {data.AppliedFiltersText}").FontSize(8).FontColor(Colors.Grey.Darken1);
                            });

                            row.ConstantItem(140).Column(c =>
                            {
                                c.Item().Text($"IST: {data.GeneratedAt.ToIstString("yyyy-MM-dd HH:mm")}")
                                    .AlignRight().FontSize(8);
                                c.Item().Text("Official System Audit")
                                    .AlignRight().FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
                            });
                        });
                        col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Colors.Blue.Darken3);
                    });

                    // Content
                    page.Content().PaddingTop(12).Column(col =>
                    {
                        // Dynamic KPI Summary Bar
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Period Revenue:").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"₹{data.SummaryStats.TotalSalesRevenue:N2}").Bold().FontSize(11).FontColor(Colors.Green.Darken3);
                            });
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Total Orders:").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"{data.SummaryStats.TotalOrders} Invoices").Bold().FontSize(11);
                            });
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Asset Valuation:").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"₹{data.SummaryStats.CurrentInventoryValue:N2}").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                            });
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Stock Alerts:").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"{data.SummaryStats.LowStockCount} Low / {data.SummaryStats.OutOfStockCount} Out").Bold().FontSize(10).FontColor(Colors.Orange.Darken3);
                            });
                        });

                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(25);
                                cols.RelativeColumn(2.5f);
                                cols.RelativeColumn(2.5f);
                                cols.RelativeColumn(2.0f);
                                cols.RelativeColumn(1.8f);
                                cols.RelativeColumn(1.8f);
                            });

                            table.Header(h =>
                            {
                                int cIdx = 0;
                                foreach (var head in data.Headers.Take(6))
                                {
                                    h.Cell().Background(Colors.Blue.Darken4).Padding(4).Text(head).Bold().FontColor(Colors.White);
                                    cIdx++;
                                }
                            });

                            int rowIdx = 1;
                            foreach (var item in data.Rows)
                            {
                                var bg = rowIdx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Background(bg).Padding(4).Text(rowIdx.ToString());
                                table.Cell().Background(bg).Padding(4).Text(string.IsNullOrWhiteSpace(item.PrimaryText) ? item.Code : item.PrimaryText).Bold();
                                table.Cell().Background(bg).Padding(4).Text(string.IsNullOrWhiteSpace(item.Name) ? item.CustomerInfo : item.Name);
                                table.Cell().Background(bg).Padding(4).Text(string.IsNullOrWhiteSpace(item.CategoryName) ? item.DateString : item.CategoryName);
                                table.Cell().Background(bg).Padding(4).Text(item.GrandTotal > 0 ? $"₹{item.GrandTotal:N2}" : item.Quantity.ToString()).AlignRight();
                                table.Cell().Background(bg).Padding(4).Text(string.IsNullOrWhiteSpace(item.BadgeText) ? (item.UserExecutor ?? "-") : item.BadgeText).AlignCenter();

                                rowIdx++;
                            }
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

        // Legacy compatibility overrides
        public byte[] GenerateSalesExcelReport(DateTime start, DateTime end, IEnumerable<Sale> sales)
        {
            var req = new ReportFilterRequest { ReportType = "Sales", StartDate = start, EndDate = end, DatePreset = "Custom" };
            var data = BuildReportDataAsync(req).GetAwaiter().GetResult();
            return GenerateExcelReport(data);
        }

        public byte[] GenerateInventoryValuationExcelReport(IEnumerable<Product> products, Dictionary<string, string> categoryNames)
        {
            var req = new ReportFilterRequest { ReportType = "Inventory", DatePreset = "AllTime" };
            var data = BuildReportDataAsync(req).GetAwaiter().GetResult();
            return GenerateExcelReport(data);
        }

        public byte[] GenerateInventoryPdfReport(IEnumerable<Product> products, Dictionary<string, string> categoryNames)
        {
            var req = new ReportFilterRequest { ReportType = "Inventory", DatePreset = "AllTime" };
            var data = BuildReportDataAsync(req).GetAwaiter().GetResult();
            return GeneratePdfReport(data);
        }

        #endregion
    }
}
