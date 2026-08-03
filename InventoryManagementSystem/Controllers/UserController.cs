using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize(Roles = Role.Admin)]
    public class UserController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;
        private readonly IAuditLogService _auditLogService;

        public UserController(
            IUserRepository userRepository,
            IAuthService authService,
            IAuditLogService auditLogService)
        {
            _userRepository = userRepository;
            _authService = authService;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _userRepository.GetAllAsync();
            return View(users);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
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

            model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);
            model.CreatedDate = DateTime.UtcNow;
            model.UpdatedDate = DateTime.UtcNow;

            await _userRepository.CreateAsync(model);
            await _auditLogService.LogActivityAsync("User Created", User.Identity?.Name ?? "Admin", $"User: {model.Username}", $"Registered new {model.Role} account.");

            TempData["ToastMessage"] = "User account created successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User model)
        {
            var existing = await _userRepository.GetByIdAsync(model.Id);
            if (existing == null) return NotFound();

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
            existing.Role = model.Role;
            existing.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(existing.Id, existing);
            await _auditLogService.LogActivityAsync("User Updated", User.Identity?.Name ?? "Admin", $"User ID: {model.Id}", $"Modified details for user: {model.Username}.");

            TempData["ToastMessage"] = "User account updated successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound();

            // Prevent self locking
            if (user.Username.Equals(User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ToastMessage"] = "You cannot lock your own account!";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            user.IsLocked = !user.IsLocked;
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            await _auditLogService.LogActivityAsync("User Lock Toggled", User.Identity?.Name ?? "Admin", $"User: {user.Username}", $"Locked status set to: {user.IsLocked}");

            TempData["ToastMessage"] = user.IsLocked ? "User account locked!" : "User account unlocked.";
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
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            await _auditLogService.LogActivityAsync("User Password Override", User.Identity?.Name ?? "Admin", $"User: {user.Username}", $"Overwrote password for user ID: {id}");

            TempData["ToastMessage"] = "User password updated successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }
    }
}
