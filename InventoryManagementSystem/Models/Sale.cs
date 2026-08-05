using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    public class Sale
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("InvoiceNumber")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [BsonElement("CustomerName")]
        public string CustomerName { get; set; } = string.Empty;

        [BsonElement("CustomerPhone")]
        public string CustomerPhone { get; set; } = string.Empty;

        [BsonElement("Date")]
        public DateTime Date { get; set; } = DateTime.UtcNow;

        [BsonElement("SubTotal")]
        public decimal SubTotal { get; set; }

        [BsonElement("GstPercentage")]
        public decimal GstPercentage { get; set; } = 18.0m;

        [BsonElement("GstAmount")]
        public decimal GstAmount { get; set; }

        [BsonElement("Discount")]
        public decimal Discount { get; set; }

        [BsonElement("GrandTotal")]
        public decimal GrandTotal { get; set; }

        [BsonElement("CreatedBy")]
        public string CreatedBy { get; set; } = string.Empty; // Username

        [BsonElement("PaymentStatus")]
        public string PaymentStatus { get; set; } = "Paid"; // Paid, Unpaid, Partial, Draft

        [BsonElement("AmountPaid")]
        public decimal AmountPaid { get; set; }

        [BsonElement("DueAmount")]
        public decimal DueAmount { get; set; }

        [BsonElement("CompanyGstin")]
        public string CompanyGstin { get; set; } = "27AAAAA0000A1Z5";

        [BsonElement("Items")]
        public List<SaleItem> Items { get; set; } = new List<SaleItem>();
    }

    public class SaleItem
    {
        [BsonElement("ProductId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("ProductName")]
        public string ProductName { get; set; } = string.Empty;

        [BsonElement("ProductCode")]
        public string ProductCode { get; set; } = string.Empty; // SKU code

        [BsonElement("Quantity")]
        public int Quantity { get; set; }

        [BsonElement("SellingPrice")]
        public decimal SellingPrice { get; set; }

        [BsonElement("Total")]
        public decimal Total { get; set; } // Quantity * SellingPrice
    }
}
