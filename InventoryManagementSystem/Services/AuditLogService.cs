using Microsoft.AspNetCore.Http;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(IAuditLogRepository auditLogRepository, IHttpContextAccessor httpContextAccessor)
        {
            _auditLogRepository = auditLogRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogActivityAsync(string action, string executedBy, string target, string details)
        {
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "N/A";

            var log = new AuditLog
            {
                Action = action,
                ExecutedBy = executedBy,
                Target = target,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                Details = details
            };

            await _auditLogRepository.CreateAsync(log);
        }

        public async Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int count)
        {
            return await _auditLogRepository.GetRecentLogsAsync(count);
        }
    }
}
