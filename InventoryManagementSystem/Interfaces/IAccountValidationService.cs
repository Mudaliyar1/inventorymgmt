using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IAccountValidationService
    {
        /// <summary>
        /// Checks whether an email address is already registered across ANY account type (User/Admin/Staff or Supplier).
        /// Case-insensitive comparison.
        /// </summary>
        Task<bool> IsEmailAlreadyRegisteredAsync(string email, string? excludeUserId = null, string? excludeSupplierId = null);

        /// <summary>
        /// Checks whether a username/company identifier is already registered across ANY account type (User/Admin/Staff or Supplier).
        /// Case-insensitive comparison.
        /// </summary>
        Task<bool> IsUsernameAlreadyRegisteredAsync(string username, string? excludeUserId = null, string? excludeSupplierId = null);
    }
}
