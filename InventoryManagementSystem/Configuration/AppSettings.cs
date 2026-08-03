namespace InventoryManagementSystem.Configuration
{
    public class AppSettings
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyLogoUrl { get; set; } = string.Empty;
        public string Currency { get; set; } = "INR";
        public double GstPercentage { get; set; } = 18.0;
    }
}
