using InventoryManagementSystem.Models;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels
{
    public class ProductListViewModel
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public IEnumerable<Category> Categories { get; set; } = new List<Category>();

        public string? Search { get; set; }
        public string? SelectedCategoryId { get; set; }
        public string? Brand { get; set; }
        public string? ModelName { get; set; }
        public string? StockStatus { get; set; }
        public string? StatusFilter { get; set; }
        public string? ProductSource { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? MinStock { get; set; }
        public int? MaxStock { get; set; }
        public string? SortBy { get; set; }
        public bool IsDescending { get; set; }

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public long TotalItems { get; set; }
        public int PageSize { get; set; }
    }
}
