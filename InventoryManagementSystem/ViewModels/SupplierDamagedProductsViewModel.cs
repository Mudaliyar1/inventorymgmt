using System;
using System.Collections.Generic;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.ViewModels
{
    public class SupplierDamagedProductsViewModel
    {
        public List<DamagedProductItemViewModel> Items { get; set; } = new List<DamagedProductItemViewModel>();

        // KPI Summary Cards
        public int TotalDamagedUnits { get; set; }
        public int UnderRepairUnits { get; set; }
        public int RepairedRestockedUnits { get; set; }
        public int ScrappedUnits { get; set; }
        public decimal TotalDamagedValue { get; set; }

        // Filters
        public string? Search { get; set; }
        public string? RepairStatus { get; set; }
        public string? Brand { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalItems { get; set; }
        public int TotalPages => PageSize > 0 ? (int)System.Math.Ceiling((double)TotalItems / PageSize) : 1;

        public List<string> Brands { get; set; } = new List<string>();
        public List<string> RepairStatuses { get; set; } = ReturnRepairStatus.AllStatuses;
    }

    public class DamagedProductItemViewModel
    {
        public string ReturnId { get; set; } = string.Empty;
        public string ReturnNumber { get; set; } = string.Empty;
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public DateTime ReturnedDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string AdditionalRemarks { get; set; } = string.Empty;
        
        // Item Details
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Ram { get; set; } = string.Empty;
        public string Storage { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalValue { get; set; }
        public List<string> Imeis { get; set; } = new List<string>();

        // Disposition & Repair
        public string Disposition { get; set; } = ReturnDispositionType.DamagedRepair;
        public string RepairStatus { get; set; } = ReturnRepairStatus.UnderRepair;
        public string RepairNotes { get; set; } = string.Empty;
        public DateTime? RepairedAt { get; set; }
        public bool IsRestocked { get; set; }
    }
}
