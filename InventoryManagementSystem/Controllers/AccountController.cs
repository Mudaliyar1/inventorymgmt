using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IImageService _imageService;
        private readonly IAuditLogService _auditLogService;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly IPermissionService _permissionService;
        private readonly ISupplierService _supplierService;

        public AccountController(
            IAuthService authService,
            IImageService imageService,
            IAuditLogService auditLogService,
            IEmailService emailService,
            IUserRepository userRepository,
            IPermissionService permissionService,
            ISupplierService supplierService)
        {
            _authService = authService;
            _imageService = imageService;
            _auditLogService = auditLogService;
            _emailService = emailService;
            _userRepository = userRepository;
            _permissionService = permissionService;
            _supplierService = supplierService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole(Role.Supplier))
                {
                    return RedirectToAction("Index", "SupplierDashboard");
                }
                return RedirectToAction("Index", "Home");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Try Authenticating User (Admin / Staff)
            var user = await _authService.AuthenticateAsync(model.UsernameOrEmail, model.Password);
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("FullName", user.FullName),
                    new Claim("EmployeeId", !string.IsNullOrEmpty(user.EmployeeId) ? user.EmployeeId : "EMP-0000"),
                    new Claim("ProfilePictureUrl", string.IsNullOrEmpty(user.ProfilePictureUrl) ? "/images/default-avatar.png" : user.ProfilePictureUrl)
                };

                if (user.Permissions != null)
                {
                    foreach (var perm in user.Permissions)
                    {
                        claims.Add(new Claim("Permission", perm));
                    }
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(120)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                await _auditLogService.LogExAsync(user.Role == Role.Admin ? "Admin Login" : "Employee Login", "Authentication", $"{user.FullName} (@{user.Username})", $"User authenticated successfully with role '{user.Role}'.", "Success", "Success", referenceId: user.Id);

                TempData["ToastMessage"] = $"Welcome back, {user.FullName}!";
                TempData["ToastType"] = "success";

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            // 2. Try Authenticating Supplier Vendor Account
            var supplier = await _supplierService.AuthenticateSupplierAsync(model.UsernameOrEmail, model.Password);
            if (supplier != null)
            {
                var supplierClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, supplier.Id),
                    new Claim(ClaimTypes.Name, supplier.CompanyName),
                    new Claim(ClaimTypes.Email, supplier.Email),
                    new Claim(ClaimTypes.Role, Role.Supplier),
                    new Claim("FullName", string.IsNullOrEmpty(supplier.ContactPerson) ? supplier.CompanyName : supplier.ContactPerson),
                    new Claim("CompanyName", supplier.CompanyName),
                    new Claim("SupplierId", supplier.Id)
                };

                var claimsIdentity = new ClaimsIdentity(supplierClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(120)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                await _auditLogService.LogExAsync("SUPPLIER_LOGIN", "Authentication", supplier.CompanyName, $"Supplier '{supplier.CompanyName}' ({supplier.Email}) logged in successfully.", "Success", "Success", referenceId: supplier.Id);

                TempData["ToastMessage"] = $"Welcome to SIMS Supplier Portal, {supplier.CompanyName}!";
                TempData["ToastType"] = "success";

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                return RedirectToAction("Index", "SupplierDashboard");
            }

            await _auditLogService.LogExAsync(
                "Failed Login", "Authentication", model.UsernameOrEmail,
                $"Failed login attempt for identifier '{model.UsernameOrEmail}'. Invalid credentials or account deactivated.",
                "Failed", "Warning");

            ModelState.AddModelError(string.Empty, "Invalid login attempt. Check credentials or account status.");
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var username = User.Identity.Name ?? "Unknown";
                await _auditLogService.LogExAsync("Employee Logout", "Authentication", username, "User terminated session and logged out.", "Success", "Information");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["ToastMessage"] = "Logged out successfully.";
            TempData["ToastType"] = "info";

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var token = await _authService.GeneratePasswordResetTokenAsync(model.Email);
            if (token != null)
            {
                var resetLink = Url.Action("ResetPassword", "Account", new { email = model.Email, token = token }, Request.Scheme);
                if (!string.IsNullOrEmpty(resetLink))
                {
                    await _emailService.SendForgotPasswordEmailAsync(model.Email, resetLink);
                }
            }

            ViewBag.Message = "If that email address exists in our system, we have sent a reset password link to it.";
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            var model = new ResetPasswordViewModel { Email = email, Token = token };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var success = await _authService.ResetPasswordAsync(model.Email, model.Token, model.Password);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Failed to reset password. The link may have expired or is invalid.");
                return View(model);
            }

            await _auditLogService.LogActivityAsync("Password Reset", model.Email, model.Email, "User successfully reset their password.");
            await _emailService.SendPasswordChangedEmailAsync(model.Email, model.Email);

            TempData["ToastMessage"] = "Password reset successful. You can now log in.";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Login));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();

            var model = new ProfileViewModel
            {
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                CurrentProfilePictureUrl = string.IsNullOrEmpty(user.ProfilePictureUrl) ? "/images/default-avatar.png" : user.ProfilePictureUrl
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();

            // Populate non-posted read-only fields for redisplaying form
            model.Username = user.Username;
            model.Email = user.Email;
            model.Role = user.Role;
            model.CurrentProfilePictureUrl = string.IsNullOrEmpty(user.ProfilePictureUrl) ? "/images/default-avatar.png" : user.ProfilePictureUrl;

            // Remove password validation check if no password update is attempted
            if (string.IsNullOrEmpty(model.CurrentPassword) && string.IsNullOrEmpty(model.NewPassword))
            {
                ModelState.Remove(nameof(model.CurrentPassword));
                ModelState.Remove(nameof(model.NewPassword));
                ModelState.Remove(nameof(model.ConfirmNewPassword));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? uploadedImageUrl = null;
            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                var uploadResult = await _imageService.UploadImageAsync(model.ProfileImage, "users_profiles");
                if (!uploadResult.IsSuccess)
                {
                    ModelState.AddModelError(nameof(model.ProfileImage), uploadResult.ErrorMessage);
                    return View(model);
                }
                uploadedImageUrl = uploadResult.SecureUrl;
            }

            // Update basic profile details
            var profileUpdated = await _authService.UpdateProfileAsync(userId, model.FullName, model.PhoneNumber, uploadedImageUrl);
            if (!profileUpdated)
            {
                ModelState.AddModelError(string.Empty, "Failed to update profile details.");
                return View(model);
            }

            // Change password if requested
            if (!string.IsNullOrEmpty(model.CurrentPassword) && !string.IsNullOrEmpty(model.NewPassword))
            {
                var pwdChanged = await _authService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
                if (!pwdChanged)
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                    return View(model);
                }
                await _auditLogService.LogActivityAsync("Password Changed", user.Username, $"User ID: {user.Id}", "User successfully changed their password.");
                await _emailService.SendPasswordChangedEmailAsync(user.Email, user.Username);
            }

            await _auditLogService.LogActivityAsync("Profile Updated", user.Username, $"User ID: {user.Id}", "User updated their profile details.");

            // Refresh authentication cookie to reflect new profile pic/fullname in UI immediately
            var refreshedUser = await _userRepository.GetByIdAsync(userId);
            if (refreshedUser != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, refreshedUser.Id),
                    new Claim(ClaimTypes.Name, refreshedUser.Username),
                    new Claim(ClaimTypes.Email, refreshedUser.Email),
                    new Claim(ClaimTypes.Role, refreshedUser.Role),
                    new Claim("FullName", refreshedUser.FullName),
                    new Claim("ProfilePictureUrl", string.IsNullOrEmpty(refreshedUser.ProfilePictureUrl) ? "/images/default-avatar.png" : refreshedUser.ProfilePictureUrl)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
            }

            TempData["ToastMessage"] = "Profile updated successfully!";
            TempData["ToastType"] = "success";

            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet("/api/account/live-status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLiveStatus()
        {
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Ok(new { authenticated = false, isLocked = false, isDeleted = false, version = 0 });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Ok(new { authenticated = false, isLocked = false, isDeleted = false, version = 0 });
            }

            if (User.IsInRole(Role.Supplier))
            {
                var supplier = await _supplierService.GetSupplierByIdAsync(userId);
                if (supplier == null || supplier.Status == "Inactive")
                {
                    return Ok(new { authenticated = true, isLocked = supplier?.Status == "Inactive", isDeleted = supplier == null, version = 1 });
                }
                return Ok(new { authenticated = true, isLocked = false, isDeleted = false, version = 1 });
            }

            var state = await _permissionService.GetLiveUserStateAsync(userId);
            return Ok(new
            {
                authenticated = true,
                isLocked = state.IsLocked,
                isDeleted = !state.IsValid,
                version = state.PermissionVersion
            });
        }
    }
}
