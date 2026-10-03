using System.Collections.Generic;

namespace InventoryManagementSystem.Interfaces
{
    public class PasswordValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();

        public bool HasMinLength { get; set; }
        public bool HasUppercase { get; set; }
        public bool HasLowercase { get; set; }
        public bool HasNumber { get; set; }
        public bool HasSpecialCharacter { get; set; }
        public bool PasswordsMatch { get; set; } = true;
        public bool IsDifferentFromCurrent { get; set; } = true;

        public string GetCombinedErrorMessage() => string.Join(" ", Errors);
    }

    public interface IPasswordPolicyService
    {
        int MinimumLength { get; }
        bool RequireUppercase { get; }
        bool RequireLowercase { get; }
        bool RequireNumber { get; }
        bool RequireSpecialCharacter { get; }

        PasswordValidationResult Validate(
            string? password,
            string? confirmPassword = null,
            string? currentPasswordHash = null,
            bool isConfirmRequired = false);

        bool IsValid(string? password);
        string ComputeTokenHash(string rawToken);
        string GenerateSecureToken();
    }
}
