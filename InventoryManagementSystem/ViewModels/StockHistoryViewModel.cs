using Microsoft.AspNetCore.Mvc.Rendering;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels
{
    public class StockHistoryViewModel
    {
        public IEnumerable<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();
        public Dictionary<string, string> ProductNames { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ProductCodes { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ProductCategories { get; set; } = new Dictionary<string, string>();

        // Filter Options
        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Products { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> TransactionTypes { get; set; } = new List<SelectListItem>();

        // Active Filter Criteria
        public string? SearchTerm { get; set; }
        public string? Type { get; set; }
        public string? CategoryId { get; set; }
        public string? ProductId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? ExecutedBy { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public long TotalItems { get; set; }
        public int PageSize { get; set; } = 15;
    }
}
