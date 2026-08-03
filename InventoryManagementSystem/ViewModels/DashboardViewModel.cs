using InventoryManagementSystem.Models;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalCategories { get; set; }
        public int TotalProducts { get; set; }
        public int CurrentStock { get; set; }
        public decimal TodaysSales { get; set; }
        public decimal MonthlySales { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }

        public IEnumerable<AuditLog> RecentActivities { get; set; } = new List<AuditLog>();
        public IEnumerable<Notification> UnreadNotifications { get; set; } = new List<Notification>();
    }
}
