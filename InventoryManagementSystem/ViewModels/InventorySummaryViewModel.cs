using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels
{
    public class InventoryItemSummaryViewModel
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = "General";
        public string? ImageUrl { get; set; }

        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int CurrentStock { get; set; }
        public int MinimumStock { get; set; }
        public string StockStatus { get; set; } = "Healthy"; // Healthy, Low Stock, Out of Stock

        public decimal CostValuation => CurrentStock * PurchasePrice;
        public decimal RetailValuation => CurrentStock * SellingPrice;
        public decimal PotentialProfit => RetailValuation - CostValuation;

        public int TotalUnitsSold { get; set; }
        public decimal TotalSalesRevenue { get; set; }
    }

    public class InventorySummaryViewModel
    {
        // High-level inventory statistics
        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalStockQuantity { get; set; }
        public decimal TotalCostValuation { get; set; }
        public decimal TotalRetailValuation { get; set; }
        public decimal TotalPotentialProfit => TotalRetailValuation - TotalCostValuation;
        public double MarginPercentage => TotalCostValuation > 0 ? (double)((TotalPotentialProfit / TotalCostValuation) * 100) : 0;

        public int HealthyCount { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }

        // Filter & Pagination properties
        public string? SearchTerm { get; set; }
        public string? CategoryId { get; set; }
        public string? StockStatus { get; set; }
        public string? SortBy { get; set; }

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> StockStatuses { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> SortOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<InventoryItemSummaryViewModel> Items { get; set; } = new List<InventoryItemSummaryViewModel>();

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public long TotalItems { get; set; }
        public int TotalPages { get; set; } = 1;
    }
}
