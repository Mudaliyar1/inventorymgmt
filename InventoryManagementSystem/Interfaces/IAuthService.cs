using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IAuthService
    {
        Task<User?> AuthenticateAsync(string usernameOrEmail, string password);
        Task<bool> RegisterUserAsync(User user, string password);
        Task<string?> GeneratePasswordResetTokenAsync(string email);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
        Task<(bool Success, string Message)> GeneratePasswordResetOtpAsync(string email);
        Task<(bool Success, string Message)> ResetPasswordWithOtpAsync(string email, string otp, string newPassword);
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> UpdateProfileAsync(string userId, string fullName, string phoneNumber, string? profilePictureUrl);
    }
}
