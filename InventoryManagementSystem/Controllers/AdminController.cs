using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize(Roles = Role.Admin)]
    public class AdminController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuditLogService _auditLogService;
        private readonly IPermissionService _permissionService;

        public AdminController(
            IUserRepository userRepository,
            IAuditLogService auditLogService,
            IPermissionService permissionService)
        {
            _userRepository = userRepository;
            _auditLogService = auditLogService;
            _permissionService = permissionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var allUsers = await _userRepository.GetAllAsync();
            var admins = allUsers.Where(u => u.Role == Role.Admin).OrderByDescending(u => u.CreatedDate);
            return View(admins);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var nextAdminId = $"ADM-{Random.Shared.Next(1000, 9999)}";
            return View(new User { EmployeeId = nextAdminId, Role = Role.Admin });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User model, string rawPassword)
        {
            var existingByEmail = await _userRepository.GetByEmailAsync(model.Email);
            if (existingByEmail != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email address is already registered.");
            }

            var existingByUsername = await _userRepository.GetByUsernameAsync(model.Username);
            if (existingByUsername != null)
            {
                ModelState.AddModelError(nameof(model.Username), "Username is already taken.");
            }

            if (string.IsNullOrWhiteSpace(rawPassword) || rawPassword.Length < 6)
            {
                ModelState.AddModelError(nameof(rawPassword), "Password must be at least 6 characters.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.EmployeeId))
            {
                model.EmployeeId = $"ADM-{Random.Shared.Next(1000, 9999)}";
            }

            model.Role = Role.Admin;
            model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);
            model.Permissions = new List<string>();
            model.PermissionVersion = 1;
            model.CreatedDate = DateTime.UtcNow;
            model.UpdatedDate = DateTime.UtcNow;

            await _userRepository.CreateAsync(model);
            await _auditLogService.LogEmployeeActivityAsync(
                "Administrator Registered", "Administrator Management", $"Admin: {model.FullName} ({model.Username})",
                $"Created new Super Administrator account (ID: {model.EmployeeId}) with full unrestricted system privileges."
            );

            TempData["ToastMessage"] = $"Super Administrator account ({model.EmployeeId}) created successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null || user.Role != Role.Admin) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User model)
        {
            var existing = await _userRepository.GetByIdAsync(model.Id);
            if (existing == null || existing.Role != Role.Admin) return NotFound();

            var existingByEmail = await _userRepository.GetByEmailAsync(model.Email);
            if (existingByEmail != null && existingByEmail.Id != model.Id)
            {
                ModelState.AddModelError(nameof(model.Email), "Email address is already in use by another user.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            existing.FullName = model.FullName;
            existing.Email = model.Email;
            existing.PhoneNumber = model.PhoneNumber;
            existing.EmployeeId = !string.IsNullOrWhiteSpace(model.EmployeeId) ? model.EmployeeId : existing.EmployeeId;
            existing.PermissionVersion++;
            existing.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(existing.Id, existing);
            _permissionService.InvalidateUserCache(existing.Id);

            await _auditLogService.LogEmployeeActivityAsync(
                "Administrator Details Updated", "Administrator Management", $"Admin: {existing.Username}",
                $"Updated profile information for administrator {existing.FullName} ({existing.EmployeeId})."
            );

            TempData["ToastMessage"] = $"Administrator account ({existing.EmployeeId}) updated successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null || user.Role != Role.Admin) return NotFound();

            if (user.Username.Equals(User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ToastMessage"] = "You cannot lock or deactivate your own active Administrator account!";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            user.IsLocked = !user.IsLocked;
            user.PermissionVersion++;
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            _permissionService.InvalidateUserCache(user.Id);

            await _auditLogService.LogEmployeeActivityAsync(
                user.IsLocked ? "Administrator Deactivated" : "Administrator Activated", "Administrator Management", $"Admin: {user.Username}",
                $"Account active status set to: {(!user.IsLocked ? "Active" : "Locked/Deactivated")}"
            );

            TempData["ToastMessage"] = user.IsLocked ? $"Administrator {user.FullName} account locked." : $"Administrator {user.FullName} account unlocked.";
            TempData["ToastType"] = "info";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null || user.Role != Role.Admin) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword, string confirmPassword)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null || user.Role != Role.Admin) return NotFound();

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError(string.Empty, "Password must be at least 6 characters.");
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
            }

            if (!ModelState.IsValid)
            {
                return View(user);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PermissionVersion++;
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            _permissionService.InvalidateUserCache(user.Id);

            await _auditLogService.LogEmployeeActivityAsync(
                "Administrator Password Reset", "Administrator Management", $"Admin: {user.Username}",
                $"Administrator password reset completed for {user.FullName} ({user.EmployeeId})."
            );

            TempData["ToastMessage"] = $"Password for administrator {user.FullName} updated successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Activity(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null || user.Role != Role.Admin) return NotFound();

            var logs = await _auditLogService.GetLogsByEmployeeAsync(user.Username, 100);
            ViewBag.AdminUser = user;
            return View(logs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null || user.Role != Role.Admin) return NotFound();

            if (user.Username.Equals(User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ToastMessage"] = "You cannot delete your own active Administrator account!";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            await _userRepository.DeleteAsync(id);
            _permissionService.InvalidateUserCache(id);

            await _auditLogService.LogEmployeeActivityAsync(
                "Administrator Account Deleted", "Administrator Management", $"Admin: {user.Username}",
                $"Deleted administrator account {user.FullName} (ID: {user.EmployeeId})."
            );

            TempData["ToastMessage"] = $"Administrator {user.FullName} deleted successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BulkDelete([FromBody] List<string> ids)
        {
            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "No administrator accounts selected for deletion." });
            }

            int deletedCount = 0;
            var currentUsername = User.Identity?.Name ?? string.Empty;

            foreach (var id in ids)
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user != null && user.Role == Role.Admin && !user.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
                {
                    await _userRepository.DeleteAsync(id);
                    _permissionService.InvalidateUserCache(id);
                    deletedCount++;
                }
            }

            await _auditLogService.LogEmployeeActivityAsync(
                "Bulk Administrators Deleted", "Administrator Management", "Multiple Admins",
                $"Bulk deleted {deletedCount} administrator account(s)."
            );

            return Json(new { success = true, count = deletedCount, message = $"{deletedCount} administrator account(s) deleted successfully." });
        }
    }
}
