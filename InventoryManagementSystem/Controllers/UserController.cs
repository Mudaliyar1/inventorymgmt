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
    public class UserController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;
        private readonly IAuditLogService _auditLogService;
        private readonly IPermissionDiscoveryService _permissionDiscovery;
        private readonly IPermissionService _permissionService;
        private readonly IAccountValidationService _accountValidationService;

        public UserController(
            IUserRepository userRepository,
            IAuthService authService,
            IAuditLogService auditLogService,
            IPermissionDiscoveryService permissionDiscovery,
            IPermissionService permissionService,
            IAccountValidationService accountValidationService)
        {
            _userRepository = userRepository;
            _authService = authService;
            _auditLogService = auditLogService;
            _permissionDiscovery = permissionDiscovery;
            _permissionService = permissionService;
            _accountValidationService = accountValidationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var allUsers = await _userRepository.GetAllAsync();
            var employees = allUsers.Where(u => u.Role != Role.Admin).OrderByDescending(u => u.CreatedDate);
            return View(employees);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.GroupedPermissions = _permissionDiscovery.GetGroupedPermissions();
            var nextEmpId = $"EMP-{Random.Shared.Next(1000, 9999)}";
            return View(new User { EmployeeId = nextEmpId, Role = Role.Staff });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User model, string rawPassword, List<string> selectedPermissions)
        {
            ViewBag.GroupedPermissions = _permissionDiscovery.GetGroupedPermissions();

            if (!string.IsNullOrWhiteSpace(model.Email) && await _accountValidationService.IsEmailAlreadyRegisteredAsync(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "This email address is already registered with another account.");
            }

            if (!string.IsNullOrWhiteSpace(model.Username) && await _accountValidationService.IsUsernameAlreadyRegisteredAsync(model.Username))
            {
                ModelState.AddModelError(nameof(model.Username), "This username or identifier is already taken by another account.");
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
                model.EmployeeId = $"EMP-{Random.Shared.Next(1000, 9999)}";
            }

            model.Role = Role.Staff;
            model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);
            model.Permissions = (selectedPermissions ?? new List<string>())
                .Where(p => !p.StartsWith("Admin.", StringComparison.OrdinalIgnoreCase) && !p.StartsWith("User.", StringComparison.OrdinalIgnoreCase))
                .ToList();
            model.PermissionVersion = 1;
            model.LastPermissionUpdated = DateTime.UtcNow;
            model.CreatedDate = DateTime.UtcNow;
            model.UpdatedDate = DateTime.UtcNow;

            await _userRepository.CreateAsync(model);
            await _auditLogService.LogEmployeeActivityAsync(
                "Employee Added", "Employee Management", $"Employee: {model.FullName} ({model.Username})",
                $"Created new employee account (ID: {model.EmployeeId}) with {model.Permissions.Count} granted permissions.",
                previousData: "",
                newData: $"ID: {model.EmployeeId}, Role: {model.Role}, Email: {model.Email}"
            );

            TempData["ToastMessage"] = $"Employee account ({model.EmployeeId}) created successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();

            ViewBag.GroupedPermissions = _permissionDiscovery.GetGroupedPermissions();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User model, List<string> selectedPermissions)
        {
            ViewBag.GroupedPermissions = _permissionDiscovery.GetGroupedPermissions();

            var existing = await _userRepository.GetByIdAsync(model.Id);
            if (existing == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                ModelState.AddModelError(nameof(model.FullName), "Full Name is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email address is required.");
            }
            else
            {
                if (await _accountValidationService.IsEmailAlreadyRegisteredAsync(model.Email.Trim(), excludeUserId: model.Id))
                {
                    ModelState.AddModelError(nameof(model.Email), "This email address is already registered with another account.");
                }
            }

            // Remove all model state errors except FullName and Email
            var keysToRemove = ModelState.Keys
                .Where(k => !k.Equals(nameof(model.FullName), StringComparison.OrdinalIgnoreCase) &&
                            !k.Equals(nameof(model.Email), StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                ModelState.Remove(key);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var oldSummary = $"FullName: {existing.FullName}, Role: {existing.Role}, Active: {!existing.IsLocked}, Perms: {existing.Permissions?.Count ?? 0}";

            existing.FullName = model.FullName;
            existing.Email = model.Email;
            existing.PhoneNumber = model.PhoneNumber;
            existing.Role = Role.Staff;
            existing.EmployeeId = !string.IsNullOrWhiteSpace(model.EmployeeId) ? model.EmployeeId : existing.EmployeeId;
            existing.Permissions = (selectedPermissions ?? new List<string>())
                .Where(p => !p.StartsWith("Admin.", StringComparison.OrdinalIgnoreCase) && !p.StartsWith("User.", StringComparison.OrdinalIgnoreCase))
                .ToList();
            existing.PermissionVersion++;
            existing.LastPermissionUpdated = DateTime.UtcNow;
            existing.UpdatedDate = DateTime.UtcNow;

            var newSummary = $"FullName: {existing.FullName}, Role: {existing.Role}, Active: {!existing.IsLocked}, Perms: {existing.Permissions.Count}";

            await _userRepository.UpdateAsync(existing.Id, existing);
            _permissionService.InvalidateUserCache(existing.Id);

            await _auditLogService.LogEmployeeActivityAsync(
                "Employee Permissions & Details Updated", "Employee Management", $"Employee ID: {existing.EmployeeId} ({existing.Username})",
                $"Updated details and permission assignments for employee {existing.FullName}.",
                previousData: oldSummary,
                newData: newSummary
            );

            TempData["ToastMessage"] = $"Employee account ({existing.EmployeeId}) updated successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();

            if (user.Username.Equals(User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ToastMessage"] = "You cannot deactivate your own Super Admin account!";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            user.IsLocked = !user.IsLocked;
            user.PermissionVersion++;
            user.LastPermissionUpdated = DateTime.UtcNow;
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            _permissionService.InvalidateUserCache(user.Id);

            await _auditLogService.LogEmployeeActivityAsync(
                user.IsLocked ? "Employee Deactivated" : "Employee Activated", "Employee Management", $"Employee: {user.Username}",
                $"Account active status changed to: {(!user.IsLocked ? "Active" : "Deactivated/Locked")}"
            );

            TempData["ToastMessage"] = user.IsLocked ? $"Employee {user.FullName} deactivated." : $"Employee {user.FullName} activated!";
            TempData["ToastType"] = "info";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword, string confirmPassword)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();

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
            user.LastPermissionUpdated = DateTime.UtcNow;
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            _permissionService.InvalidateUserCache(user.Id);

            await _auditLogService.LogEmployeeActivityAsync(
                "Employee Password Reset", "Employee Management", $"Employee: {user.Username}",
                $"Administrator reset password for employee {user.FullName} ({user.EmployeeId})."
            );

            TempData["ToastMessage"] = $"Password for employee {user.FullName} updated successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Activity(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();

            var logs = await _auditLogService.GetLogsByEmployeeAsync(user.Username, 100);
            ViewBag.Employee = user;
            return View(logs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();

            if (user.Username.Equals(User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ToastMessage"] = "You cannot delete your own active account!";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            await _userRepository.DeleteAsync(id);
            _permissionService.InvalidateUserCache(id);

            await _auditLogService.LogEmployeeActivityAsync(
                "Employee Deleted", "Employee Management", $"Employee: {user.Username}",
                $"Deleted employee account {user.FullName} (ID: {user.EmployeeId})."
            );

            TempData["ToastMessage"] = $"Employee {user.FullName} deleted successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BulkDelete([FromBody] List<string> ids)
        {
            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "No employee accounts selected for deletion." });
            }

            int deletedCount = 0;
            var currentUsername = User.Identity?.Name ?? string.Empty;

            foreach (var id in ids)
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user != null && !user.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
                {
                    await _userRepository.DeleteAsync(id);
                    _permissionService.InvalidateUserCache(id);
                    deletedCount++;
                }
            }

            await _auditLogService.LogEmployeeActivityAsync(
                "Bulk Employees Deleted", "Employee Management", "Multiple Employees",
                $"Bulk deleted {deletedCount} employee account(s)."
            );

            return Json(new { success = true, count = deletedCount, message = $"{deletedCount} employee account(s) deleted successfully." });
        }
    }
}
