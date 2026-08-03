using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;

        public AuthService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
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
