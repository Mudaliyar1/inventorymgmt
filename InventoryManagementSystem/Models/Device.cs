using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class Device
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("ProductId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("ProductCode")]
        public string ProductCode { get; set; } = string.Empty;

        [BsonElement("ProductName")]
        public string ProductName { get; set; } = string.Empty;

        [BsonElement("Brand")]
        public string Brand { get; set; } = string.Empty;

        [BsonElement("ModelName")]
        public string ModelName { get; set; } = string.Empty;

        [BsonElement("Variant")]
        public string Variant { get; set; } = string.Empty;

        [BsonElement("Color")]
        public string Color { get; set; } = string.Empty;

        // Physical Identification
        [BsonElement("IMEI1")]
        public string IMEI1 { get; set; } = string.Empty;

        [BsonElement("IMEI2")]
        public string IMEI2 { get; set; } = string.Empty;

        [BsonElement("SerialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        // Commercial Details
        [BsonElement("PurchasePrice")]
        public decimal PurchasePrice { get; set; }

        [BsonElement("SellingPrice")]
        public decimal SellingPrice { get; set; }

        // Device Status: InStock, Reserved, Sold, Returned, Exchanged, Damaged, UnderRepair
        [BsonElement("Status")]
        public string Status { get; set; } = "InStock";

        // Supplier Tracking
        [BsonElement("SupplierId")]
        public string SupplierId { get; set; } = string.Empty;

        [BsonElement("SupplierName")]
        public string SupplierName { get; set; } = string.Empty;

        [BsonElement("SupplierInvoiceNumber")]
        public string SupplierInvoiceNumber { get; set; } = string.Empty;

        [BsonElement("PurchaseDate")]
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

        // Customer & Sale Tracking
        [BsonElement("SoldDate")]
        public DateTime? SoldDate { get; set; }

        [BsonElement("InvoiceNumber")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [BsonElement("CustomerId")]
        public string CustomerId { get; set; } = string.Empty;

        [BsonElement("CustomerName")]
        public string CustomerName { get; set; } = string.Empty;

        [BsonElement("CustomerPhone")]
        public string CustomerPhone { get; set; } = string.Empty;

        // Warranty Tracking
        [BsonElement("WarrantyStartDate")]
        public DateTime? WarrantyStartDate { get; set; }

        [BsonElement("WarrantyEndDate")]
        public DateTime? WarrantyEndDate { get; set; }

        [BsonElement("Notes")]
        public string Notes { get; set; } = string.Empty;

        [BsonElement("CreatedBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [BsonElement("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedDate")]
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}
