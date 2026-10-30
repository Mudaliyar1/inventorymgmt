using System;

namespace InventoryManagementSystem.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly TimeZoneInfo IstTimeZone = GetIstTimeZone();

        private static TimeZoneInfo GetIstTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "India Standard Time");
                }
            }
        }

        /// <summary>
        /// Converts a UTC DateTime to Indian Standard Time (IST, UTC+5:30).
        /// </summary>
        public static DateTime ToIst(this DateTime dateTime)
        {
            var utcDateTime = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, IstTimeZone);
        }

        /// <summary>
        /// Formats a DateTime in Indian Standard Time (IST).
        /// </summary>
        public static string ToIstString(this DateTime dateTime, string format = "MMM d, yyyy HH:mm IST")
        {
            return dateTime.ToIst().ToString(format);
        }

        /// <summary>
        /// Converts a nullable UTC DateTime to Indian Standard Time (IST, UTC+5:30).
        /// </summary>
        public static DateTime? ToIst(this DateTime? dateTime)
        {
            if (!dateTime.HasValue) return null;
            return dateTime.Value.ToIst();
        }

        /// <summary>
        /// Formats a nullable DateTime in Indian Standard Time (IST).
        /// </summary>
        public static string ToIstString(this DateTime? dateTime, string format = "dd MMM yyyy, hh:mm tt IST", string nullFallback = "—")
        {
            if (!dateTime.HasValue) return nullFallback;
            return dateTime.Value.ToIstString(format);
        }

        /// <summary>
        /// Formats a DateTime for public legal policy display in Indian Standard Time (e.g. "26 October 2026, 11:30 PM IST").
        /// </summary>
        public static string ToPolicyIstDisplayString(this DateTime dateTime)
        {
            return dateTime.ToIst().ToString("dd MMMM yyyy, hh:mm tt 'IST'");
        }

        /// <summary>
        /// Formats a nullable DateTime for public legal policy display in Indian Standard Time.
        /// </summary>
        public static string ToPolicyIstDisplayString(this DateTime? dateTime, string nullFallback = "—")
        {
            if (!dateTime.HasValue) return nullFallback;
            return dateTime.Value.ToPolicyIstDisplayString();
        }
    }
}
