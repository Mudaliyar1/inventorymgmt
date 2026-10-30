using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.ViewModels
{
    public class CustomQuoteInputModel
    {
        [Required(ErrorMessage = "Contact person name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        public string ContactName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Business name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Business name must be between 2 and 150 characters.")]
        public string BusinessName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        public string? Phone { get; set; }

        public string? Message { get; set; }

        // Quantities requested
        [Range(0, 1000, ErrorMessage = "Invalid administrator count.")]
        public int AdminCount { get; set; } = 1;

        [Range(0, 50000, ErrorMessage = "Invalid employee count.")]
        public int EmployeeCount { get; set; } = 5;

        [Range(0, 50000000, ErrorMessage = "Invalid product count.")]
        public int ProductCount { get; set; } = 1000;

        [Range(0, 100000, ErrorMessage = "Invalid supplier count.")]
        public int SupplierCount { get; set; } = 10;

        [Range(0, 10000000, ErrorMessage = "Invalid device count.")]
        public int DeviceCount { get; set; } = 0;

        public List<string>? SelectedFeatureKeys { get; set; }
    }

    public class ContactInquiryInputModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Work email is required.")]
        [EmailAddress(ErrorMessage = "Please provide a valid work email address.")]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string? BusinessName { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Subject must be between 3 and 200 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message is required.")]
        [StringLength(5000, MinimumLength = 10, ErrorMessage = "Message must be at least 10 characters.")]
        public string Message { get; set; } = string.Empty;
    }
}
