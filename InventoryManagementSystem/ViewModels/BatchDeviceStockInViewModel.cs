using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels
{
    public class BatchDeviceItemRequest
    {
        public string IMEI1 { get; set; } = string.Empty;
        public string IMEI2 { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class BatchDeviceStockInRequest
    {
        public string ProductId { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public List<BatchDeviceItemRequest> Devices { get; set; } = new List<BatchDeviceItemRequest>();
    }
}
