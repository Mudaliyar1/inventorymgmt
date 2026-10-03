using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly MongoDbContext _context;
        private readonly IPasswordPolicyService _passwordPolicyService;
        private readonly IEmailService _emailService;
        private readonly IAuditLogService _auditLogService;
        private readonly IPermissionService _permissionService;
        private readonly IUserRepository _userRepository;
        private readonly ISupplierRepository _supplierRepository;

        public PasswordResetService(
            MongoDbContext context,
            IPasswordPolicyService passwordPolicyService,
            IEmailService emailService,
            IAuditLogService auditLogService,
            IPermissionService permissionService,
            IUserRepository userRepository,
            ISupplierRepository supplierRepository)
        {
            _context = context;
            _passwordPolicyService = passwordPolicyService;
            _emailService = emailService;
            _auditLogService = auditLogService;
            _permissionService = permissionService;
            _userRepository = userRepository;
            _supplierRepository = supplierRepository;
        }

        public async Task<(bool Success, string Message)> SendPasswordResetEmailForAccountAsync(
            string accountId,
            string accountType,
            string executedBy,
            string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return (false, "Invalid account identifier.");
            }

            string email = string.Empty;
            string recipientName = string.Empty;

            if (accountType.Equals("Employee", StringComparison.OrdinalIgnoreCase))
            {
                var user = await _userRepository.GetByIdAsync(accountId);
                if (user == null) return (false, "Employee account not found.");
                if (string.IsNullOrWhiteSpace(user.Email)) return (false, "Employee has no registered email address.");

                email = user.Email.Trim();
                recipientName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Username;

                await _auditLogService.LogExAsync(
                    "EMPLOYEE_PASSWORD_RESET_REQUESTED",
                    "Authentication",
                    $"{recipientName} (@{user.Username})",
                    $"Admin '{executedBy}' requested password reset email for employee {user.FullName} ({user.EmployeeId}).",
                    "Success",
                    "Information");
            }
            else if (accountType.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            {
                var supplier = await _supplierRepository.GetByIdAsync(accountId);
                if (supplier == null) return (false, "Supplier account not found.");
                if (string.IsNullOrWhiteSpace(supplier.Email)) return (false, "Supplier has no registered email address.");

                email = supplier.Email.Trim();
                recipientName = supplier.DisplayVendorName;

                await _auditLogService.LogExAsync(
                    "SUPPLIER_PASSWORD_RESET_REQUESTED",
                    "Authentication",
                    supplier.DisplayVendorName,
                    $"Admin '{executedBy}' requested password reset email for supplier {supplier.DisplayVendorName} ({supplier.DisplayCompanyName}).",
                    "Success",
                    "Information");
            }
            else
            {
                return (false, "Unsupported account type.");
            }

            return await DispatchResetTokenAndEmailAsync(accountId, accountType, email, recipientName, executedBy, baseUrl);
        }

        public async Task<(bool Success, string Message)> RequestPasswordResetByEmailAsync(
            string email,
            string baseUrl,
            string? ipAddress = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (true, "If an account exists with that email address, a password reset link has been sent.");
            }

            var cleanEmail = email.Trim();

            // 1. Check User (Admin / Employee)
            var user = await _userRepository.GetByEmailAsync(cleanEmail);
            if (user != null)
            {
                var name = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Username;
                await _auditLogService.LogExAsync(
                    "EMPLOYEE_PASSWORD_RESET_REQUESTED",
                    "Authentication",
                    name,
                    $"Self-service password reset requested for email {cleanEmail}.",
                    "Success",
                    "Information");

                await DispatchResetTokenAndEmailAsync(user.Id, "Employee", cleanEmail, name, "Self", baseUrl, ipAddress);
                return (true, "If an account exists with that email address, a password reset link has been sent.");
            }

            // 2. Check Supplier
            var supplier = await _supplierRepository.GetByEmailAsync(cleanEmail);
            if (supplier != null)
            {
                var name = supplier.DisplayVendorName;
                await _auditLogService.LogExAsync(
                    "SUPPLIER_PASSWORD_RESET_REQUESTED",
                    "Authentication",
                    name,
                    $"Self-service password reset requested for supplier email {cleanEmail}.",
                    "Success",
                    "Information");

                await DispatchResetTokenAndEmailAsync(supplier.Id, "Supplier", cleanEmail, name, "Self", baseUrl, ipAddress);
                return (true, "If an account exists with that email address, a password reset link has been sent.");
            }

            // Consistent response preventing account enumeration
            return (true, "If an account exists with that email address, a password reset link has been sent.");
        }

        public async Task<(bool IsValid, string Message, PasswordResetRequest? Request)> ValidateResetTokenAsync(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return (false, "Password reset token is required.", null);
            }

            var tokenHash = _passwordPolicyService.ComputeTokenHash(rawToken);
            var filter = Builders<PasswordResetRequest>.Filter.Eq(r => r.TokenHash, tokenHash);
            var resetRequest = await _context.PasswordResetRequests.Find(filter).SortByDescending(r => r.CreatedAt).FirstOrDefaultAsync();

            if (resetRequest == null)
            {
                await _auditLogService.LogExAsync("PASSWORD_RESET_TOKEN_INVALID", "Authentication", "Unknown", "A password reset attempt used an unknown token.", "Failed", "Warning");
                return (false, "This password reset link is invalid or has expired.", null);
            }

            if (resetRequest.UsedAt.HasValue)
            {
                await _auditLogService.LogExAsync("PASSWORD_RESET_TOKEN_INVALID", "Authentication", resetRequest.Email, "A password reset attempt used an already-used token.", "Failed", "Warning");
                return (false, "This password reset link has already been used.", null);
            }

            if (resetRequest.ExpiresAt < DateTime.UtcNow)
            {
                await _auditLogService.LogExAsync("PASSWORD_RESET_TOKEN_EXPIRED", "Authentication", resetRequest.Email, "A password reset attempt used an expired token.", "Failed", "Warning");
                return (false, "This password reset link is invalid or has expired.", null);
            }

            // Verify account still exists
            if (resetRequest.TargetType.Equals("Employee", StringComparison.OrdinalIgnoreCase))
            {
                var user = await _userRepository.GetByIdAsync(resetRequest.TargetId);
                if (user == null) return (false, "The associated employee account no longer exists.", null);
            }
            else if (resetRequest.TargetType.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            {
                var supplier = await _supplierRepository.GetByIdAsync(resetRequest.TargetId);
                if (supplier == null) return (false, "The associated supplier account no longer exists.", null);
            }

            return (true, "Token is valid.", resetRequest);
        }

        public async Task<(bool Success, string Message)> CompletePasswordResetAsync(
            string rawToken,
            string newPassword,
            string confirmPassword,
            string? ipAddress = null)
        {
            var (isValid, validationMessage, resetRequest) = await ValidateResetTokenAsync(rawToken);
            if (!isValid || resetRequest == null)
            {
                return (false, validationMessage);
            }

            // Centralized password policy validation
            var policyResult = _passwordPolicyService.Validate(newPassword, confirmPassword, isConfirmRequired: true);
            if (!policyResult.IsValid)
            {
                return (false, policyResult.GetCombinedErrorMessage());
            }

            if (resetRequest.TargetType.Equals("Employee", StringComparison.OrdinalIgnoreCase))
            {
                var user = await _userRepository.GetByIdAsync(resetRequest.TargetId);
                if (user == null) return (false, "Account not found.");

                // Check not identical to current
                if (!string.IsNullOrWhiteSpace(user.PasswordHash) && BCrypt.Net.BCrypt.Verify(newPassword.Trim(), user.PasswordHash))
                {
                    return (false, "The new password must be different from the current password.");
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword.Trim());
                user.ResetToken = string.Empty;
                user.ResetTokenExpiry = null;
                user.PermissionVersion++; // Invalidate existing sessions
                user.LastPermissionUpdated = DateTime.UtcNow;
                user.UpdatedDate = DateTime.UtcNow;

                await _userRepository.UpdateAsync(user.Id, user);
                _permissionService.InvalidateUserCache(user.Id);

                await _auditLogService.LogExAsync(
                    "EMPLOYEE_PASSWORD_RESET_COMPLETED",
                    "Authentication",
                    $"{user.FullName} (@{user.Username})",
                    $"Employee password was successfully reset via secure email link.",
                    "Success",
                    "Information");

                await _emailService.SendPasswordChangedEmailAsync(user.Email, !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Username);
            }
            else if (resetRequest.TargetType.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            {
                var supplier = await _supplierRepository.GetByIdAsync(resetRequest.TargetId);
                if (supplier == null) return (false, "Supplier account not found.");

                // Check not identical to current
                if (!string.IsNullOrWhiteSpace(supplier.PasswordHash) && BCrypt.Net.BCrypt.Verify(newPassword.Trim(), supplier.PasswordHash))
                {
                    return (false, "The new password must be different from the current password.");
                }

                supplier.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword.Trim());
                supplier.ResetToken = string.Empty;
                supplier.ResetTokenExpiry = null;
                supplier.UpdatedDate = DateTime.UtcNow;

                await _supplierRepository.UpdateAsync(supplier.Id, supplier);

                await _auditLogService.LogExAsync(
                    "SUPPLIER_PASSWORD_RESET_COMPLETED",
                    "Authentication",
                    supplier.DisplayVendorName,
                    $"Supplier password was successfully reset via secure email link.",
                    "Success",
                    "Information");

                await _emailService.SendPasswordChangedEmailAsync(supplier.Email, supplier.DisplayVendorName);
            }

            // Invalidate current and all other pending reset tokens for this account
            var updateUsed = Builders<PasswordResetRequest>.Update.Set(r => r.UsedAt, DateTime.UtcNow);
            await _context.PasswordResetRequests.UpdateManyAsync(
                Builders<PasswordResetRequest>.Filter.Eq(r => r.TargetId, resetRequest.TargetId),
                updateUsed);

            return (true, "Your password has been reset successfully. You can now log in with your new password.");
        }

        public async Task<(bool Success, string Message)> DirectChangeEmployeePasswordAsync(
            string employeeId,
            string newPassword,
            string confirmPassword,
            string executedBy)
        {
            var user = await _userRepository.GetByIdAsync(employeeId);
            if (user == null) return (false, "Employee account not found.");

            var policyResult = _passwordPolicyService.Validate(newPassword, confirmPassword, user.PasswordHash, isConfirmRequired: true);
            if (!policyResult.IsValid)
            {
                return (false, policyResult.GetCombinedErrorMessage());
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword.Trim());
            user.ResetToken = string.Empty;
            user.ResetTokenExpiry = null;
            user.PermissionVersion++; // Invalidate active user sessions
            user.LastPermissionUpdated = DateTime.UtcNow;
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            _permissionService.InvalidateUserCache(user.Id);

            // Invalidate any open reset requests
            await _context.PasswordResetRequests.UpdateManyAsync(
                Builders<PasswordResetRequest>.Filter.And(
                    Builders<PasswordResetRequest>.Filter.Eq(r => r.TargetId, user.Id),
                    Builders<PasswordResetRequest>.Filter.Eq(r => r.UsedAt, null)),
                Builders<PasswordResetRequest>.Update.Set(r => r.UsedAt, DateTime.UtcNow));

            await _auditLogService.LogExAsync(
                "EMPLOYEE_PASSWORD_CHANGED",
                "Employee Management",
                $"{user.FullName} (@{user.Username})",
                $"Administrator '{executedBy}' directly changed password for employee {user.FullName} ({user.EmployeeId}). Existing sessions invalidated.",
                "Success",
                "Information");

            return (true, $"Password for employee '{user.FullName}' updated successfully.");
        }

        public async Task<(bool Success, string Message)> DirectChangeSupplierPasswordAsync(
            string supplierId,
            string newPassword,
            string confirmPassword,
            string executedBy)
        {
            var supplier = await _supplierRepository.GetByIdAsync(supplierId);
            if (supplier == null) return (false, "Supplier account not found.");

            var policyResult = _passwordPolicyService.Validate(newPassword, confirmPassword, supplier.PasswordHash, isConfirmRequired: true);
            if (!policyResult.IsValid)
            {
                return (false, policyResult.GetCombinedErrorMessage());
            }

            supplier.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword.Trim());
            supplier.ResetToken = string.Empty;
            supplier.ResetTokenExpiry = null;
            supplier.UpdatedDate = DateTime.UtcNow;

            await _supplierRepository.UpdateAsync(supplier.Id, supplier);

            // Invalidate any open reset requests
            await _context.PasswordResetRequests.UpdateManyAsync(
                Builders<PasswordResetRequest>.Filter.And(
                    Builders<PasswordResetRequest>.Filter.Eq(r => r.TargetId, supplier.Id),
                    Builders<PasswordResetRequest>.Filter.Eq(r => r.UsedAt, null)),
                Builders<PasswordResetRequest>.Update.Set(r => r.UsedAt, DateTime.UtcNow));

            await _auditLogService.LogExAsync(
                "SUPPLIER_PASSWORD_CHANGED",
                "Supplier Management",
                supplier.DisplayVendorName,
                $"Administrator '{executedBy}' directly changed password for supplier {supplier.DisplayVendorName} ({supplier.DisplayCompanyName}).",
                "Success",
                "Information");

            return (true, $"Password for supplier '{supplier.DisplayVendorName}' updated successfully.");
        }

        private async Task<(bool Success, string Message)> DispatchResetTokenAndEmailAsync(
            string accountId,
            string accountType,
            string email,
            string recipientName,
            string executedBy,
            string baseUrl,
            string? ipAddress = null)
        {
            // Invalidate any existing active tokens for this account
            var invalidateOld = Builders<PasswordResetRequest>.Update.Set(r => r.UsedAt, DateTime.UtcNow);
            await _context.PasswordResetRequests.UpdateManyAsync(
                Builders<PasswordResetRequest>.Filter.And(
                    Builders<PasswordResetRequest>.Filter.Eq(r => r.TargetId, accountId),
                    Builders<PasswordResetRequest>.Filter.Eq(r => r.UsedAt, null)),
                invalidateOld);

            // Generate cryptographically secure token & compute hash
            var rawToken = _passwordPolicyService.GenerateSecureToken();
            var tokenHash = _passwordPolicyService.ComputeTokenHash(rawToken);

            var resetRequest = new PasswordResetRequest
            {
                TargetId = accountId,
                TargetType = accountType,
                Email = email,
                RecipientName = recipientName,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60), // 60 min expiration
                CreatedAt = DateTime.UtcNow,
                CreatedBy = executedBy,
                IpAddress = ipAddress
            };

            await _context.PasswordResetRequests.InsertOneAsync(resetRequest);

            // Also synchronize token hash to target model (backward compatibility)
            if (accountType.Equals("Employee", StringComparison.OrdinalIgnoreCase))
            {
                var user = await _userRepository.GetByIdAsync(accountId);
                if (user != null)
                {
                    user.ResetToken = tokenHash;
                    user.ResetTokenExpiry = resetRequest.ExpiresAt;
                    await _userRepository.UpdateAsync(user.Id, user);
                }
            }
            else if (accountType.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            {
                var supplier = await _supplierRepository.GetByIdAsync(accountId);
                if (supplier != null)
                {
                    supplier.ResetToken = tokenHash;
                    supplier.ResetTokenExpiry = resetRequest.ExpiresAt;
                    await _supplierRepository.UpdateAsync(supplier.Id, supplier);
                }
            }

            var cleanBaseUrl = baseUrl.TrimEnd('/');
            var resetLink = $"{cleanBaseUrl}/Account/ResetPassword?token={rawToken}";

            try
            {
                await _emailService.SendPasswordResetEmailAsync(email, recipientName, resetLink, accountType);

                await _auditLogService.LogExAsync(
                    "PASSWORD_RESET_EMAIL_SENT",
                    "Notification",
                    email,
                    $"Password reset email sent to {email} for {accountType} account '{recipientName}'.",
                    "Success",
                    "Information");

                return (true, $"A password reset email has been sent to {email}.");
            }
            catch (Exception ex)
            {
                await _auditLogService.LogExAsync(
                    "PASSWORD_RESET_EMAIL_FAILED",
                    "Notification",
                    email,
                    $"Failed to send password reset email to {email}: {ex.Message}",
                    "Failed",
                    "Warning");

                return (false, "The password reset email could not be sent. Please try again.");
            }
        }
    }
}
