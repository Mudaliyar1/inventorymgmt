using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels
{
    public class SalesListViewModel
    {
        public IEnumerable<Sale> Sales { get; set; } = new List<Sale>();

        // Filter Criteria
        public string? SearchTerm { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Cashier { get; set; }

        // Pagination Properties
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public long TotalItems { get; set; }
        public int TotalPages { get; set; } = 1;
    }
}
