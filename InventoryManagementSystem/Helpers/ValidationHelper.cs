using System;
using System.Text.RegularExpressions;

namespace InventoryManagementSystem.Helpers
{
    public static class ValidationHelper
    {
        private static readonly Regex EmailRegex = new Regex(@"^[\w\.-]+@[\w\.-]+\.\w{2,}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PhoneRegex = new Regex(@"^\+?[0-9\s\-]{10,15}$", RegexOptions.Compiled);
        private static readonly Regex ImeiRegex = new Regex(@"^\d{14,16}$", RegexOptions.Compiled);

        /// <summary>
        /// Validates email address format. Returns true if null/empty or valid RFC format.
        /// </summary>
        public static bool IsValidEmail(string? email, bool required = false)
        {
            if (string.IsNullOrWhiteSpace(email)) return !required;
            return EmailRegex.IsMatch(email.Trim());
        }

        /// <summary>
        /// Validates phone / contact number format (10 to 15 digits). Returns true if null/empty or valid format.
        /// </summary>
        public static bool IsValidPhone(string? phone, bool required = false)
        {
            if (string.IsNullOrWhiteSpace(phone)) return !required;
            var clean = phone.Trim();
            // Count digits
            int digitCount = 0;
            foreach (char c in clean)
            {
                if (char.IsDigit(c)) digitCount++;
            }
            return PhoneRegex.IsMatch(clean) && digitCount >= 10 && digitCount <= 15;
        }

        /// <summary>
        /// Validates IMEI number (strictly 14-16 numeric digits, standard 15-digit IMEI).
        /// </summary>
        public static bool IsValidImei(string? imei, bool required = false)
        {
            if (string.IsNullOrWhiteSpace(imei)) return !required;
            return ImeiRegex.IsMatch(imei.Trim());
        }
    }
}
