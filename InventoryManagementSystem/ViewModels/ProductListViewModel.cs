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
        public string? SortBy { get; set; }
        public bool IsDescending { get; set; }

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public long TotalItems { get; set; }
        public int PageSize { get; set; }
    }
}
