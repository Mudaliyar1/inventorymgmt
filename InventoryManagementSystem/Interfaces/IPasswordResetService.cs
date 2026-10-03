using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IPasswordResetService
    {
        Task<(bool Success, string Message)> SendPasswordResetEmailForAccountAsync(
            string accountId,
            string accountType, // "Employee" or "Supplier"
            string executedBy,
            string baseUrl);

        Task<(bool Success, string Message)> RequestPasswordResetByEmailAsync(
            string email,
            string baseUrl,
            string? ipAddress = null);

        Task<(bool IsValid, string Message, PasswordResetRequest? Request)> ValidateResetTokenAsync(string rawToken);

        Task<(bool Success, string Message)> CompletePasswordResetAsync(
            string rawToken,
            string newPassword,
            string confirmPassword,
            string? ipAddress = null);

        Task<(bool Success, string Message)> DirectChangeEmployeePasswordAsync(
            string employeeId,
            string newPassword,
            string confirmPassword,
            string executedBy);

        Task<(bool Success, string Message)> DirectChangeSupplierPasswordAsync(
            string supplierId,
            string newPassword,
            string confirmPassword,
            string executedBy);
    }
}
