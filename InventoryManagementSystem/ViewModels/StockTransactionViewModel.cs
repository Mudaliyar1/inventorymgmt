using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.ViewModels
{
    public class StockTransactionViewModel
    {
        [Required(ErrorMessage = "Please select a product.")]
        [Display(Name = "Select Product")]
        public string ProductId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter quantity.")]
        [Range(1, 1000000, ErrorMessage = "Quantity must be between 1 and 1,000,000.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Reason is required.")]
        public string Reason { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty; // Stock In, Stock Out, Adjustment

        public IEnumerable<SelectListItem> Products { get; set; } = new List<SelectListItem>();
    }
}
