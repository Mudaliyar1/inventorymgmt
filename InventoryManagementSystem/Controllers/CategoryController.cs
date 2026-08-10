using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class CategoryController : Controller
    {
        private readonly ICategoryService _categoryService;
        private readonly IAuditLogService _auditLogService;

        public CategoryController(ICategoryService categoryService, IAuditLogService auditLogService)
        {
            _categoryService = categoryService;
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            return View(categories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model)
        {
            model.Name = (model.Name ?? string.Empty).Trim();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await _categoryService.CreateCategoryAsync(model);
                await _auditLogService.LogExAsync("Category Added", "Categories", model.Name, $"Created new category '{model.Name}'.");

                TempData["ToastMessage"] = "Category created successfully!";
                TempData["ToastType"] = "success";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Name", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Category model)
        {
            model.Name = (model.Name ?? string.Empty).Trim();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await _categoryService.UpdateCategoryAsync(model);
                await _auditLogService.LogExAsync("Category Updated", "Categories", model.Name, $"Updated category details for '{model.Name}'.");

                TempData["ToastMessage"] = "Category updated successfully!";
                TempData["ToastType"] = "success";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Name", ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();

            await _categoryService.ToggleStatusAsync(id);
            await _auditLogService.LogExAsync("Category Status Toggled", "Categories", category.Name, $"Toggled status of category '{category.Name}'.");

            TempData["ToastMessage"] = "Category status updated!";
            TempData["ToastType"] = "info";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();

            await _categoryService.DeleteCategoryAsync(id);
            await _auditLogService.LogExAsync("Category Deleted", "Categories", category.Name, $"Deleted category '{category.Name}'.", "Success", "Warning");

            TempData["ToastMessage"] = "Category deleted successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Index));
        }
    }
}
