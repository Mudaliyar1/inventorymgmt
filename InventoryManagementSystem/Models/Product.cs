using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InventoryManagementSystem.Models
{
    public class Product
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("Code")]
        public string Code { get; set; } = string.Empty; // SKU code

        [BsonElement("Barcode")]
        public string Barcode { get; set; } = string.Empty;

        [BsonElement("CategoryId")]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonIgnoreIfNull]
        [BsonIgnoreIfDefault]
        public string? CategoryId { get; set; }

        // Mobile Shop Specific Classification
        [BsonElement("ProductType")]
        public string ProductType { get; set; } = "Accessory"; // Smartphone, Feature Phone, Tablet, Smartwatch, Accessory, Charger, Cable, Mobile Cover, Tempered Glass, Earphones, Headphones, Power Bank, Memory Card, Speaker, Other

        [BsonElement("Brand")]
        public string Brand { get; set; } = string.Empty;

        [BsonElement("ModelName")]
        public string ModelName { get; set; } = string.Empty;

        [BsonElement("ModelNumber")]
        public string ModelNumber { get; set; } = string.Empty;

        [BsonElement("Variant")]
        public string Variant { get; set; } = string.Empty; // e.g., 8GB RAM / 128GB Storage

        [BsonElement("Color")]
        public string Color { get; set; } = string.Empty;

        // Embedded Detailed Hardware Specifications Sub-Model
        [BsonElement("Specs")]
        public MobileSpecifications Specs { get; set; } = new MobileSpecifications();

        // Legacy / Convenience Hardware Specification Shortcuts
        [BsonElement("Ram")]
        public string Ram { get; set; } = string.Empty;

        [BsonElement("Storage")]
        public string Storage { get; set; } = string.Empty;

        [BsonElement("Processor")]
        public string Processor
        {
            get => !string.IsNullOrWhiteSpace(Specs?.ProcessorName) ? Specs.ProcessorName : _processor;
            set { _processor = value; if (Specs != null && string.IsNullOrWhiteSpace(Specs.ProcessorName)) Specs.ProcessorName = value; }
        }
        private string _processor = string.Empty;

        [BsonElement("DisplaySize")]
        public string DisplaySize
        {
            get => !string.IsNullOrWhiteSpace(Specs?.DisplaySize) ? Specs.DisplaySize : _displaySize;
            set { _displaySize = value; if (Specs != null && string.IsNullOrWhiteSpace(Specs.DisplaySize)) Specs.DisplaySize = value; }
        }
        private string _displaySize = string.Empty;

        [BsonElement("BatteryCapacity")]
        public string BatteryCapacity
        {
            get => (Specs != null && Specs.BatteryCapacityMah > 0) ? $"{Specs.BatteryCapacityMah} mAh" : _batteryCapacity;
            set { _batteryCapacity = value; }
        }
        private string _batteryCapacity = string.Empty;

        [BsonElement("OperatingSystem")]
        public string OperatingSystem
        {
            get => !string.IsNullOrWhiteSpace(Specs?.OperatingSystem) ? Specs.OperatingSystem : _operatingSystem;
            set { _operatingSystem = value; if (Specs != null && string.IsNullOrWhiteSpace(Specs.OperatingSystem)) Specs.OperatingSystem = value; }
        }
        private string _operatingSystem = string.Empty;

        [BsonElement("NetworkSupport")]
        public string NetworkSupport
        {
            get => (Specs != null && Specs.Network5G) ? "5G / 4G / VoLTE" : _networkSupport;
            set { _networkSupport = value; }
        }
        private string _networkSupport = string.Empty;

        [BsonElement("SimType")]
        public string SimType
        {
            get => !string.IsNullOrWhiteSpace(Specs?.SimType) ? Specs.SimType : _simType;
            set { _simType = value; if (Specs != null && string.IsNullOrWhiteSpace(Specs.SimType)) Specs.SimType = value; }
        }
        private string _simType = string.Empty;

        // Commercials
        [BsonElement("PurchasePrice")]
        public decimal PurchasePrice { get; set; }

        [BsonElement("SellingPrice")]
        public decimal SellingPrice { get; set; }

        [BsonElement("Mrp")]
        public decimal Mrp { get; set; }

        [BsonElement("GstPercentage")]
        public decimal GstPercentage { get; set; } = 18.0m;

        [BsonElement("CurrentStock")]
        public int CurrentStock { get; set; }

        [BsonElement("MinimumStock")]
        public int MinimumStock { get; set; }

        [BsonElement("WarrantyDurationMonths")]
        public int WarrantyDurationMonths { get; set; } = 12;

        [BsonElement("IsImeiRequired")]
        public bool IsImeiRequired { get; set; } = false;

        [BsonElement("Description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("ImageUrl")]
        public string ImageUrl { get; set; } = string.Empty;

        [BsonElement("ImagePublicId")]
        public string ImagePublicId { get; set; } = string.Empty;

        [BsonElement("ImageOriginalFilename")]
        public string ImageOriginalFilename { get; set; } = string.Empty;

        [BsonElement("Status")]
        public string Status { get; set; } = "Active"; // Active, Inactive

        [BsonElement("LastAlertSentType")]
        public string LastAlertSentType { get; set; } = "None"; // None, LowStock, OutOfStock

        [BsonElement("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedDate")]
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}
