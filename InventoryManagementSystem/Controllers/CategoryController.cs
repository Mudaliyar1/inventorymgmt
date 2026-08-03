using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _categoryService.CreateCategoryAsync(model);
            await _auditLogService.LogActivityAsync("Category Added", User.Identity?.Name ?? "System", $"Category Name: {model.Name}", "Created new category.");

            TempData["ToastMessage"] = "Category created successfully!";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Index));
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _categoryService.UpdateCategoryAsync(model);
            await _auditLogService.LogActivityAsync("Category Updated", User.Identity?.Name ?? "System", $"Category ID: {model.Id}", $"Updated category details for {model.Name}.");

            TempData["ToastMessage"] = "Category updated successfully!";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();

            await _categoryService.ToggleStatusAsync(id);
            await _auditLogService.LogActivityAsync("Category Status Toggled", User.Identity?.Name ?? "System", $"Category ID: {id}", $"Toggled status of {category.Name}.");

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
            await _auditLogService.LogActivityAsync("Category Deleted", User.Identity?.Name ?? "System", $"Category: {category.Name}", $"Deleted category ID: {id}.");

            TempData["ToastMessage"] = "Category deleted successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Index));
        }
    }
}
