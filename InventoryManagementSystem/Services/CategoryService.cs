using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            return await _categoryRepository.GetAllAsync();
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            return await _categoryRepository.FindAsync(c => c.Status == "Active");
        }

        public async Task<Category?> GetCategoryByIdAsync(string id)
        {
            return await _categoryRepository.GetByIdAsync(id);
        }

        public async Task CreateCategoryAsync(Category category)
        {
            category.CreatedDate = DateTime.UtcNow;
            category.UpdatedDate = DateTime.UtcNow;
            await _categoryRepository.CreateAsync(category);
        }

        public async Task UpdateCategoryAsync(Category category)
        {
            var existing = await _categoryRepository.GetByIdAsync(category.Id);
            if (existing != null)
            {
                existing.Name = category.Name;
                existing.Description = category.Description;
                existing.Status = category.Status;
                existing.UpdatedDate = DateTime.UtcNow;
                await _categoryRepository.UpdateAsync(existing.Id, existing);
            }
        }

        public async Task DeleteCategoryAsync(string id)
        {
            await _categoryRepository.DeleteAsync(id);
        }

        public async Task ToggleStatusAsync(string id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category != null)
            {
                category.Status = category.Status == "Active" ? "Inactive" : "Active";
                category.UpdatedDate = DateTime.UtcNow;
                await _categoryRepository.UpdateAsync(category.Id, category);
            }
        }
    }
}
