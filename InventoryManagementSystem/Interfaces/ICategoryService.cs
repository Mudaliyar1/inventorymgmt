using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface ICategoryService
    {
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<IEnumerable<Category>> GetActiveCategoriesAsync();
        Task<IEnumerable<Category>> GetCategoriesForUserAsync(string? supplierId);
        Task<IEnumerable<Category>> GetActiveCategoriesForUserAsync(string? supplierId);
        Task<Category?> GetCategoryByIdAsync(string id);
        Task CreateCategoryAsync(Category category);
        Task UpdateCategoryAsync(Category category);
        Task DeleteCategoryAsync(string id);
        Task ToggleStatusAsync(string id);
    }
}
