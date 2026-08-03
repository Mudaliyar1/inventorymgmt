namespace InventoryManagementSystem.DTOs
{
    public class ImageUploadResult
    {
        public string PublicId { get; set; } = string.Empty;
        public string SecureUrl { get; set; } = string.Empty;
        public string OriginalFilename { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
