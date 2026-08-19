using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<IEnumerable<Category>> GetCategoriesForUserAsync(string? supplierId)
        {
            if (string.IsNullOrEmpty(supplierId))
            {
                return await _categoryRepository.FindAsync(c => c.SupplierId == null);
            }
            return await _categoryRepository.FindAsync(c => c.SupplierId == supplierId);
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesForUserAsync(string? supplierId)
        {
            if (string.IsNullOrEmpty(supplierId))
            {
                return await _categoryRepository.FindAsync(c => c.SupplierId == null && c.Status == "Active");
            }
            return await _categoryRepository.FindAsync(c => c.SupplierId == supplierId && c.Status == "Active");
        }

        public async Task<Category?> GetCategoryByIdAsync(string id)
        {
            return await _categoryRepository.GetByIdAsync(id);
        }

        public async Task CreateCategoryAsync(Category category)
        {
            category.Name = (category.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                throw new InvalidOperationException("Category Name is required.");
            }

            IEnumerable<Category> existing;
            if (string.IsNullOrEmpty(category.SupplierId))
            {
                existing = await _categoryRepository.FindAsync(c => c.SupplierId == null && c.Name.ToLower() == category.Name.ToLower());
            }
            else
            {
                existing = await _categoryRepository.FindAsync(c => c.SupplierId == category.SupplierId && c.Name.ToLower() == category.Name.ToLower());
            }

            if (existing.Any())
            {
                throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
            }

            category.CreatedDate = DateTime.UtcNow;
            category.UpdatedDate = DateTime.UtcNow;

            try
            {
                await _categoryRepository.CreateAsync(category);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey || ex.WriteError?.Code == 11000)
            {
                throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
            }
            catch (MongoBulkWriteException ex) when (ex.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey || e.Code == 11000))
            {
                throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
            }
            catch (MongoException ex) when (ex.Message.Contains("11000") || ex.Message.Contains("DuplicateKey") || ex.Message.Contains("dup key"))
            {
                throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
            }
        }

        public async Task UpdateCategoryAsync(Category category)
        {
            category.Name = (category.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                throw new InvalidOperationException("Category Name is required.");
            }

            IEnumerable<Category> duplicate;
            if (string.IsNullOrEmpty(category.SupplierId))
            {
                duplicate = await _categoryRepository.FindAsync(c => c.Id != category.Id && c.SupplierId == null && c.Name.ToLower() == category.Name.ToLower());
            }
            else
            {
                duplicate = await _categoryRepository.FindAsync(c => c.Id != category.Id && c.SupplierId == category.SupplierId && c.Name.ToLower() == category.Name.ToLower());
            }

            if (duplicate.Any())
            {
                throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
            }

            var existing = await _categoryRepository.GetByIdAsync(category.Id);
            if (existing != null)
            {
                existing.Name = category.Name;
                existing.Description = category.Description;
                existing.Status = category.Status;
                existing.UpdatedDate = DateTime.UtcNow;

                try
                {
                    await _categoryRepository.UpdateAsync(existing.Id, existing);
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey || ex.WriteError?.Code == 11000)
                {
                    throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
                }
                catch (MongoBulkWriteException ex) when (ex.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey || e.Code == 11000))
                {
                    throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
                }
                catch (MongoException ex) when (ex.Message.Contains("11000") || ex.Message.Contains("DuplicateKey") || ex.Message.Contains("dup key"))
                {
                    throw new InvalidOperationException($"A category with the name '{category.Name}' already exists.");
                }
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
