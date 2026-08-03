using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.DTOs;
using ImageUploadResult = InventoryManagementSystem.DTOs.ImageUploadResult;
using InventoryManagementSystem.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class CloudinaryImageService : IImageService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryImageService(IOptions<CloudinarySettings> config)
        {
            var acc = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(acc);
        }

        public async Task<ImageUploadResult> UploadImageAsync(IFormFile file, string folder)
        {
            var result = new ImageUploadResult();

            if (file == null || file.Length == 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "File is empty.";
                return result;
            }

            var extension = Path.GetExtension(file.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (Array.IndexOf(allowedExtensions, extension) < 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Only JPG, JPEG, PNG, and WEBP files are allowed.";
                return result;
            }

            if (file.Length > 5 * 1024 * 1024) // 5MB limit
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Maximum allowed image size is 5MB.";
                return result;
            }

            try
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = uploadResult.Error.Message;
                    return result;
                }

                result.IsSuccess = true;
                result.PublicId = uploadResult.PublicId;
                result.SecureUrl = uploadResult.SecureUrl.ToString();
                result.OriginalFilename = file.FileName;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"An error occurred during Cloudinary upload: {ex.Message}";
            }

            return result;
        }

        public async Task<bool> DeleteImageAsync(string publicId)
        {
            if (string.IsNullOrEmpty(publicId))
                return false;

            try
            {
                var deleteParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deleteParams);
                return result.Result == "ok";
            }
            catch
            {
                return false;
            }
        }

        public async Task<ImageUploadResult> ReplaceImageAsync(string oldPublicId, IFormFile newFile, string folder)
        {
            if (!string.IsNullOrEmpty(oldPublicId))
            {
                await DeleteImageAsync(oldPublicId);
            }
            return await UploadImageAsync(newFile, folder);
        }
    }
}
