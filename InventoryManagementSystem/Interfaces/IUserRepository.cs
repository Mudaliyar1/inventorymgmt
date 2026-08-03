using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IUserRepository : IBaseRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByUsernameAsync(string username);
    }
}
