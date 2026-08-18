using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.ViewModels
{
    public class ProductEditViewModel
    {
        public string? Id { get; set; }

        [Required(ErrorMessage = "Product Name is required.")]
        [Display(Name = "Product Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product Code (SKU) is required.")]
        [Display(Name = "Product Code (SKU)")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Barcode is required.")]
        public string Barcode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        [Display(Name = "Category")]
        public string CategoryId { get; set; } = string.Empty;

        public string ProductType { get; set; } = "Smartphone";
        public string Brand { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        [Required(ErrorMessage = "Purchase Price is required.")]
        [Range(0.01, 10000000.0, ErrorMessage = "Price must be greater than zero.")]
        [Display(Name = "Purchase Price (₹)")]
        public decimal PurchasePrice { get; set; }

        [Required(ErrorMessage = "Selling Price is required.")]
        [Range(0.01, 10000000.0, ErrorMessage = "Price must be greater than zero.")]
        [Display(Name = "Selling Price (₹)")]
        public decimal SellingPrice { get; set; }

        [Required(ErrorMessage = "Minimum Stock level is required.")]
        [Range(0, 1000000, ErrorMessage = "Minimum Stock must be non-negative.")]
        [Display(Name = "Minimum Stock Level")]
        public int MinimumStock { get; set; }

        public string? Description { get; set; }

        public string? CurrentImageUrl { get; set; }

        [Display(Name = "Product Image")]
        public IFormFile? ProductImage { get; set; }

        public MobileSpecifications Specs { get; set; } = new MobileSpecifications();

        public string Status { get; set; } = "Active";

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }
}
