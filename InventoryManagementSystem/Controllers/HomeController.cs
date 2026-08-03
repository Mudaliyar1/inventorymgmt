using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly IAuditLogService _auditLogService;
        private readonly INotificationRepository _notificationRepository;

        public HomeController(
            ICategoryRepository categoryRepository,
            IProductRepository productRepository,
            ISaleRepository saleRepository,
            IAuditLogService auditLogService,
            INotificationRepository notificationRepository)
        {
            _categoryRepository = categoryRepository;
            _productRepository = productRepository;
            _saleRepository = saleRepository;
            _auditLogService = auditLogService;
            _notificationRepository = notificationRepository;
        }

        public async Task<IActionResult> Index()
        {
            // Fetch live data from MongoDB
            var categories = await _categoryRepository.GetAllAsync();
            var products = (await _productRepository.GetAllAsync()).ToList();
            var sales = (await _saleRepository.GetAllAsync()).ToList();

            // Category and Product Metrics
            int totalCategories = categories.Count();
            int totalProducts = products.Count();
            int currentStock = products.Sum(p => p.CurrentStock);
            int lowStockCount = products.Count(p => p.Status == "Active" && p.CurrentStock <= p.MinimumStock);
            int outOfStockCount = products.Count(p => p.Status == "Active" && p.CurrentStock == 0);

            // Time windows
            var todayUtc = DateTime.UtcNow.Date;
            var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            // Sales and Revenue calculations
            decimal todaysSales = sales.Where(s => s.Date >= todayUtc).Sum(s => s.GrandTotal);
            decimal monthlySales = sales.Where(s => s.Date >= firstOfMonth).Sum(s => s.GrandTotal);
            decimal totalRevenue = monthlySales; // Revenue is defined as Monthly Sales on dashboard card

            // Profit Calculation
            decimal monthlyProfit = 0;
            var monthlySalesList = sales.Where(s => s.Date >= firstOfMonth);
            foreach (var sale in monthlySalesList)
            {
                foreach (var item in sale.Items)
                {
                    var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null)
                    {
                        monthlyProfit += (item.SellingPrice - product.PurchasePrice) * item.Quantity;
                    }
                    else
                    {
                        // Fallback estimate of 20% margin if product is deleted
                        monthlyProfit += (item.SellingPrice * 0.20m) * item.Quantity;
                    }
                }
            }

            var recentLogs = await _auditLogService.GetRecentActivityAsync(6);
            var unreadNotifications = await _notificationRepository.GetUnreadNotificationsAsync();

            var viewModel = new DashboardViewModel
            {
                TotalCategories = totalCategories,
                TotalProducts = totalProducts,
                CurrentStock = currentStock,
                TodaysSales = todaysSales,
                MonthlySales = monthlySales,
                Revenue = totalRevenue,
                Profit = monthlyProfit,
                LowStockCount = lowStockCount,
                OutOfStockCount = outOfStockCount,
                RecentActivities = recentLogs,
                UnreadNotifications = unreadNotifications
            };

            return View(viewModel);
        }

        [AllowAnonymous]
        public IActionResult Error()
        {
            return View();
        }
    }
}
