using MongoDB.Bson.Serialization.Attributes;

namespace InventoryManagementSystem.Models
{
    public class MobileSpecifications
    {
        // Performance & Processor
        [BsonElement("ProcessorBrand")]
        public string ProcessorBrand { get; set; } = string.Empty; // Apple, Qualcomm, MediaTek, Samsung Exynos, Google Tensor, Unisoc

        [BsonElement("ProcessorName")]
        public string ProcessorName { get; set; } = string.Empty; // e.g. Snapdragon 8 Gen 3, A16 Bionic, Dimensity 9300

        [BsonElement("Chipset")]
        public string Chipset { get; set; } = string.Empty;

        [BsonElement("CpuCores")]
        public string CpuCores { get; set; } = string.Empty; // Octa-Core, Hexa-Core, Quad-Core

        [BsonElement("CpuClockSpeed")]
        public string CpuClockSpeed { get; set; } = string.Empty; // e.g. 3.3 GHz

        [BsonElement("Gpu")]
        public string Gpu { get; set; } = string.Empty; // e.g. Adreno 750, Immortalis-G720

        // Memory & Storage
        [BsonElement("RamType")]
        public string RamType { get; set; } = string.Empty; // LPDDR5X, LPDDR5, LPDDR4X

        [BsonElement("StorageType")]
        public string StorageType { get; set; } = string.Empty; // UFS 4.0, UFS 3.1, eMMC 5.1, NVMe

        [BsonElement("ExpandableStorage")]
        public bool ExpandableStorage { get; set; } = false;

        [BsonElement("MaxExpandableStorage")]
        public string MaxExpandableStorage { get; set; } = string.Empty; // Up to 1TB

        // Display
        [BsonElement("DisplaySize")]
        public string DisplaySize { get; set; } = string.Empty; // e.g. 6.7 inches

        [BsonElement("DisplayType")]
        public string DisplayType { get; set; } = string.Empty; // AMOLED, Super Retina XDR, IPS LCD

        [BsonElement("Resolution")]
        public string Resolution { get; set; } = string.Empty; // 1440 x 3120 pixels, FHD+

        [BsonElement("RefreshRate")]
        public string RefreshRate { get; set; } = string.Empty; // 120Hz, 144Hz, 90Hz, 60Hz

        [BsonElement("ScreenProtection")]
        public string ScreenProtection { get; set; } = string.Empty; // Corning Gorilla Glass Victus 2, Ceramic Shield

        [BsonElement("Touchscreen")]
        public bool Touchscreen { get; set; } = true;

        // Camera
        [BsonElement("RearCameraCount")]
        public int RearCameraCount { get; set; } = 3;

        [BsonElement("PrimaryRearCameraMp")]
        public string PrimaryRearCameraMp { get; set; } = string.Empty; // e.g. 200 MP, 50 MP, 48 MP

        [BsonElement("UltrawideCameraMp")]
        public string UltrawideCameraMp { get; set; } = string.Empty; // e.g. 12 MP

        [BsonElement("TelephotoCameraMp")]
        public string TelephotoCameraMp { get; set; } = string.Empty; // e.g. 10 MP, 50 MP

        [BsonElement("FrontCameraCount")]
        public int FrontCameraCount { get; set; } = 1;

        [BsonElement("FrontCameraMp")]
        public string FrontCameraMp { get; set; } = string.Empty; // e.g. 12 MP, 32 MP

        [BsonElement("CameraFeatures")]
        public string CameraFeatures { get; set; } = string.Empty; // OIS, EIS, 8K Video, 4K 60fps, Night Mode

        // Battery & Charging
        [BsonElement("BatteryCapacityMah")]
        public int BatteryCapacityMah { get; set; } = 5000;

        [BsonElement("FastChargingWattage")]
        public string FastChargingWattage { get; set; } = string.Empty; // e.g. 45W, 67W, 120W

        [BsonElement("WirelessCharging")]
        public bool WirelessCharging { get; set; } = false;

        [BsonElement("ChargingPortType")]
        public string ChargingPortType { get; set; } = "USB Type-C"; // USB Type-C, Lightning, Micro-USB

        // Network & Connectivity
        [BsonElement("Network5G")]
        public bool Network5G { get; set; } = true;

        [BsonElement("Network4G")]
        public bool Network4G { get; set; } = true;

        [BsonElement("VoLte")]
        public bool VoLte { get; set; } = true;

        [BsonElement("WifiVersion")]
        public string WifiVersion { get; set; } = "Wi-Fi 6E";

        [BsonElement("BluetoothVersion")]
        public string BluetoothVersion { get; set; } = "Bluetooth 5.3";

        [BsonElement("Nfc")]
        public bool Nfc { get; set; } = true;

        [BsonElement("SimType")]
        public string SimType { get; set; } = "Dual SIM";

        [BsonElement("EsimSupport")]
        public bool EsimSupport { get; set; } = false;

        // Operating System & Security
        [BsonElement("OperatingSystem")]
        public string OperatingSystem { get; set; } = "Android"; // Android, iOS, KaiOS

        [BsonElement("OsVersion")]
        public string OsVersion { get; set; } = string.Empty; // e.g. Android 14, iOS 17

        [BsonElement("FingerprintSensor")]
        public string FingerprintSensor { get; set; } = "In-Display Ultrasonic"; // In-Display Ultrasonic, Side-Mounted, Rear, None

        [BsonElement("FaceUnlock")]
        public bool FaceUnlock { get; set; } = true;

        [BsonElement("WaterResistance")]
        public string WaterResistance { get; set; } = "IP68"; // IP68, IP67, IP54, None

        [BsonElement("AudioFeatures")]
        public string AudioFeatures { get; set; } = "Stereo Speakers, Dolby Atmos";

        // Physical Specs & Colors
        [BsonElement("OfficialColors")]
        public string OfficialColors { get; set; } = string.Empty; // e.g. Titanium Gray, Cosmic Black, Ocean Blue

        [BsonElement("DimensionsWeight")]
        public string DimensionsWeight { get; set; } = string.Empty; // e.g. 160.2 x 75.1 x 7.9 mm, 197g

        [BsonElement("Sensors")]
        public string Sensors { get; set; } = string.Empty; // e.g. Accelerometer, Gyroscope, Proximity, Compass, Barometer
    }
}
