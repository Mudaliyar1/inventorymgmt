using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using InventoryManagementSystem.DTOs;

namespace InventoryManagementSystem.Interfaces
{
    public interface IImageService
    {
        Task<ImageUploadResult> UploadImageAsync(IFormFile file, string folder);
        Task<bool> DeleteImageAsync(string publicId);
        Task<ImageUploadResult> ReplaceImageAsync(string oldPublicId, IFormFile newFile, string folder);
    }
}
