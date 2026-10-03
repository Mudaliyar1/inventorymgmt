using InventoryManagementSystem.Interfaces;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace InventoryManagementSystem.Services
{
    public class PasswordPolicyService : IPasswordPolicyService
    {
        public int MinimumLength => 8;
        public bool RequireUppercase => true;
        public bool RequireLowercase => true;
        public bool RequireNumber => true;
        public bool RequireSpecialCharacter => true;

        private static readonly Regex RegexUpper = new Regex(@"[A-Z]", RegexOptions.Compiled);
        private static readonly Regex RegexLower = new Regex(@"[a-z]", RegexOptions.Compiled);
        private static readonly Regex RegexDigit = new Regex(@"[0-9]", RegexOptions.Compiled);
        private static readonly Regex RegexSpecial = new Regex(@"[^A-Za-z0-9]", RegexOptions.Compiled);
        private static readonly Regex RegexFullPolicy = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$", RegexOptions.Compiled);

        public PasswordValidationResult Validate(
            string? password,
            string? confirmPassword = null,
            string? currentPasswordHash = null,
            bool isConfirmRequired = false)
        {
            var result = new PasswordValidationResult();

            if (string.IsNullOrWhiteSpace(password))
            {
                result.Errors.Add("Password is required.");
                result.HasMinLength = false;
                result.HasUppercase = false;
                result.HasLowercase = false;
                result.HasNumber = false;
                result.HasSpecialCharacter = false;
                return result;
            }

            var trimmed = password.Trim();

            // 1. Length check
            result.HasMinLength = trimmed.Length >= MinimumLength;
            if (!result.HasMinLength)
            {
                result.Errors.Add($"Password must be at least {MinimumLength} characters long.");
            }

            // 2. Uppercase check
            result.HasUppercase = RegexUpper.IsMatch(trimmed);
            if (!result.HasUppercase)
            {
                result.Errors.Add("Password must contain at least one uppercase letter (A-Z).");
            }

            // 3. Lowercase check
            result.HasLowercase = RegexLower.IsMatch(trimmed);
            if (!result.HasLowercase)
            {
                result.Errors.Add("Password must contain at least one lowercase letter (a-z).");
            }

            // 4. Number check
            result.HasNumber = RegexDigit.IsMatch(trimmed);
            if (!result.HasNumber)
            {
                result.Errors.Add("Password must contain at least one number (0-9).");
            }

            // 5. Special character check
            result.HasSpecialCharacter = RegexSpecial.IsMatch(trimmed);
            if (!result.HasSpecialCharacter)
            {
                result.Errors.Add("Password must contain at least one special character (e.g. !@#$%^&*()_-+=?).");
            }

            // 6. Confirm password check
            if (isConfirmRequired || confirmPassword != null)
            {
                result.PasswordsMatch = string.Equals(trimmed, confirmPassword?.Trim(), StringComparison.Ordinal);
                if (!result.PasswordsMatch)
                {
                    result.Errors.Add("Passwords do not match.");
                }
            }

            // 7. Not identical to current password check
            if (!string.IsNullOrWhiteSpace(currentPasswordHash))
            {
                try
                {
                    if (BCrypt.Net.BCrypt.Verify(trimmed, currentPasswordHash))
                    {
                        result.IsDifferentFromCurrent = false;
                        result.Errors.Add("The new password must be different from the current password.");
                    }
                }
                catch
                {
                    // Ignore BCrypt verification failure on corrupted hash
                }
            }

            return result;
        }

        public bool IsValid(string? password)
        {
            if (string.IsNullOrWhiteSpace(password)) return false;
            return RegexFullPolicy.IsMatch(password.Trim());
        }

        public string ComputeTokenHash(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken)) return string.Empty;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim().ToLowerInvariant()));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public string GenerateSecureToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }
    }
}
