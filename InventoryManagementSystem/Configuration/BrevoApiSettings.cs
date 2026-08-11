namespace InventoryManagementSystem.Configuration
{
    public class BrevoApiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = "noreply@sims.com";
        public string SenderName { get; set; } = "SIMS Inventory System";
    }
}
