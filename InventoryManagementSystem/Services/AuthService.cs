using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IEmailService _emailService;
        private readonly IAuditLogService _auditLogService;

        public AuthService(
            IUserRepository userRepository,
            ISupplierRepository supplierRepository,
            IEmailService emailService,
            IAuditLogService auditLogService)
        {
            _userRepository = userRepository;
            _supplierRepository = supplierRepository;
            _emailService = emailService;
            _auditLogService = auditLogService;
        }

        public async Task<User?> AuthenticateAsync(string usernameOrEmail, string password)
        {
            User? user = null;

            if (usernameOrEmail.Contains("@"))
            {
                user = await _userRepository.GetByEmailAsync(usernameOrEmail);
            }
            else
            {
                user = await _userRepository.GetByUsernameAsync(usernameOrEmail);
            }

            if (user == null || user.IsLocked)
            {
                return null;
            }

            // Verify password using BCrypt
            bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!isValid)
            {
                return null;
            }

            return user;
        }

        public async Task<bool> RegisterUserAsync(User user, string password)
        {
            // Check if user already exists
            var existingByEmail = await _userRepository.GetByEmailAsync(user.Email);
            if (existingByEmail != null) return false;

            var existingByUsername = await _userRepository.GetByUsernameAsync(user.Username);
            if (existingByUsername != null) return false;

            // Hash password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.CreatedDate = DateTime.UtcNow;
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.CreateAsync(user);
            return true;
        }

        public async Task<string?> GeneratePasswordResetTokenAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null) return null;

            var token = Guid.NewGuid().ToString("N");
            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);

            await _userRepository.UpdateAsync(user.Id, user);
            return token;
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null || user.ResetToken != token || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                return false;
            }

            // Hash new password and clear token
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.ResetToken = string.Empty;
            user.ResetTokenExpiry = null;
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            return true;
        }

        public async Task<(bool Success, string Message)> GeneratePasswordResetOtpAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Please enter your registered email address.");
            }

            var cleanEmail = email.Trim();
            var otp = Random.Shared.Next(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(15);

            // 1. Check User collection (Admin, Employee)
            var user = await _userRepository.GetByEmailAsync(cleanEmail);
            if (user != null)
            {
                user.ResetToken = otp;
                user.ResetTokenExpiry = expiry;
                await _userRepository.UpdateAsync(user.Id, user);

                await _emailService.SendPasswordResetOtpEmailAsync(user.Email, otp, !string.IsNullOrEmpty(user.FullName) ? user.FullName : user.Username);
                await _auditLogService.LogActivityAsync("PASSWORD_RESET_OTP_SENT", user.Username, user.Id, $"OTP generated and dispatched to {user.Email}");
                return (true, "A 6-digit verification OTP has been sent to your registered email address.");
            }

            // 2. Check Supplier collection (Distributor / Supplier)
            var supplier = await _supplierRepository.GetByEmailAsync(cleanEmail);
            if (supplier != null)
            {
                supplier.ResetToken = otp;
                supplier.ResetTokenExpiry = expiry;
                await _supplierRepository.UpdateAsync(supplier.Id, supplier);

                var recipient = !string.IsNullOrEmpty(supplier.ContactPerson) ? supplier.ContactPerson : supplier.CompanyName;
                await _emailService.SendPasswordResetOtpEmailAsync(supplier.Email, otp, recipient);
                await _auditLogService.LogActivityAsync("PASSWORD_RESET_OTP_SENT", supplier.CompanyName, supplier.Id, $"OTP generated and dispatched to {supplier.Email}");
                return (true, "A 6-digit verification OTP has been sent to your registered email address.");
            }

            // Provide consistent user feedback for security
            return (true, "If that email is registered in our system, a 6-digit verification OTP has been sent to it.");
        }

        public async Task<(bool Success, string Message)> ResetPasswordWithOtpAsync(string email, string otp, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Email address is required.");
            }
            if (string.IsNullOrWhiteSpace(otp) || otp.Trim().Length != 6)
            {
                return (false, "Please enter the valid 6-digit numeric OTP.");
            }
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                return (false, "New password must be at least 8 characters long.");
            }

            var cleanEmail = email.Trim();
            var cleanOtp = otp.Trim();

            // 1. Check User (Admin, Employee)
            var user = await _userRepository.GetByEmailAsync(cleanEmail);
            if (user != null)
            {
                if (string.IsNullOrWhiteSpace(user.ResetToken) || !string.Equals(user.ResetToken, cleanOtp, StringComparison.Ordinal))
                {
                    return (false, "Invalid OTP code. Please check the code received in your email.");
                }

                if (!user.ResetTokenExpiry.HasValue || user.ResetTokenExpiry.Value < DateTime.UtcNow)
                {
                    return (false, "This OTP has expired. Please request a new OTP.");
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.ResetToken = string.Empty;
                user.ResetTokenExpiry = null;
                user.UpdatedDate = DateTime.UtcNow;

                await _userRepository.UpdateAsync(user.Id, user);
                await _auditLogService.LogActivityAsync("PASSWORD_RESET_SUCCESS", user.Username, user.Id, "Password successfully reset via OTP verification.");
                await _emailService.SendPasswordChangedEmailAsync(user.Email, !string.IsNullOrEmpty(user.FullName) ? user.FullName : user.Username);

                return (true, "Password reset successfully. You can now log in with your new password.");
            }

            // 2. Check Supplier / Distributor
            var supplier = await _supplierRepository.GetByEmailAsync(cleanEmail);
            if (supplier != null)
            {
                if (string.IsNullOrWhiteSpace(supplier.ResetToken) || !string.Equals(supplier.ResetToken, cleanOtp, StringComparison.Ordinal))
                {
                    return (false, "Invalid OTP code. Please check the code received in your email.");
                }

                if (!supplier.ResetTokenExpiry.HasValue || supplier.ResetTokenExpiry.Value < DateTime.UtcNow)
                {
                    return (false, "This OTP has expired. Please request a new OTP.");
                }

                supplier.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                supplier.ResetToken = string.Empty;
                supplier.ResetTokenExpiry = null;
                supplier.UpdatedDate = DateTime.UtcNow;

                await _supplierRepository.UpdateAsync(supplier.Id, supplier);
                await _auditLogService.LogActivityAsync("PASSWORD_RESET_SUCCESS", supplier.CompanyName, supplier.Id, "Supplier password successfully reset via OTP verification.");
                await _emailService.SendPasswordChangedEmailAsync(supplier.Email, !string.IsNullOrEmpty(supplier.ContactPerson) ? supplier.ContactPerson : supplier.CompanyName);

                return (true, "Password reset successfully. You can now log in with your new password.");
            }

            return (false, "Account not found or invalid request.");
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            // Verify current password
            bool isValid = BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash);
            if (!isValid) return false;

            // Hash and update new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            return true;
        }

        public async Task<bool> UpdateProfileAsync(string userId, string fullName, string phoneNumber, string? profilePictureUrl)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;
            if (!string.IsNullOrEmpty(profilePictureUrl))
            {
                user.ProfilePictureUrl = profilePictureUrl;
            }
            user.UpdatedDate = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);
            return true;
        }
    }
}
