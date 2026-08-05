using Microsoft.AspNetCore.Http;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace InventoryManagementSystem.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;

        public AuditLogService(
            IAuditLogRepository auditLogRepository,
            IHttpContextAccessor httpContextAccessor,
            IUserRepository userRepository)
        {
            _auditLogRepository = auditLogRepository;
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
        }

        public async Task LogActivityAsync(string action, string executedBy, string target, string details)
        {
            await LogEmployeeActivityAsync(action, "System", target, details);
        }

        public async Task LogEmployeeActivityAsync(
            string action,
            string module,
            string target,
            string details,
            string previousData = "",
            string newData = "")
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";

            var (browser, device) = ParseUserAgent(userAgent);

            string username = httpContext?.User?.Identity?.Name ?? "System";
            string employeeId = "EMP-0000";
            string employeeName = username;

            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _userRepository.GetByIdAsync(userId);
                    if (user != null)
                    {
                        username = user.Username;
                        employeeId = !string.IsNullOrEmpty(user.EmployeeId) ? user.EmployeeId : $"EMP-{(user.Id.Length > 6 ? user.Id[..6] : user.Id)}";
                        employeeName = user.FullName;
                    }
                }
            }

            var log = new AuditLog
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                Username = username,
                Action = action,
                ExecutedBy = $"{employeeName} ({username})",
                Module = module,
                Target = target,
                PreviousData = previousData,
                NewData = newData,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                Browser = browser,
                Device = device,
                Details = details
            };

            await _auditLogRepository.CreateAsync(log);
        }

        public async Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int count)
        {
            return await _auditLogRepository.GetRecentLogsAsync(count);
        }

        public async Task<IEnumerable<AuditLog>> GetLogsByEmployeeAsync(string username, int count = 50)
        {
            var allLogs = await _auditLogRepository.GetRecentLogsAsync(500);
            return allLogs.Where(l =>
                (l.Username != null && l.Username.Equals(username, StringComparison.OrdinalIgnoreCase)) ||
                (l.ExecutedBy != null && l.ExecutedBy.Contains(username, StringComparison.OrdinalIgnoreCase))
            );
        }

        private static (string Browser, string Device) ParseUserAgent(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return ("Unknown Browser", "Unknown Device");

            string browser = "Browser";
            if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "Microsoft Edge";
            else if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) browser = "Google Chrome";
            else if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) browser = "Mozilla Firefox";
            else if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) browser = "Apple Safari";
            else if (userAgent.Contains("Trident/", StringComparison.OrdinalIgnoreCase)) browser = "Internet Explorer";

            string device = "Desktop";
            if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)) device = "Mobile Device";
            else if (userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) device = "Tablet Device";
            else if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) device = "Windows PC";
            else if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) device = "macOS Workstation";
            else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) device = "Linux Workstation";

            return (browser, device);
        }
    }
}
