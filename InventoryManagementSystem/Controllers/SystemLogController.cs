using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class SystemLogController : Controller
    {
        private readonly IAuditLogService _auditLogService;

        public SystemLogController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? keyword,
            string? module,
            [FromQuery(Name = "action")] string? logAction,
            string? status,
            string? logLevel,
            string? employee,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1,
            int pageSize = 20)
        {
            // Sanitize string parameters
            keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
            module = string.IsNullOrWhiteSpace(module) ? null : module.Trim();
            string? actionFilter = string.IsNullOrWhiteSpace(logAction) ? null : logAction.Trim();
            if (string.Equals(actionFilter, "Index", StringComparison.OrdinalIgnoreCase)) actionFilter = null;

            status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
            logLevel = string.IsNullOrWhiteSpace(logLevel) ? null : logLevel.Trim();
            employee = string.IsNullOrWhiteSpace(employee) ? null : employee.Trim();

            // Sanitize invalid dates (e.g. 0001-01-01)
            if (startDate.HasValue && startDate.Value.Year < 2000) startDate = null;
            if (endDate.HasValue && endDate.Value.Year < 2000) endDate = null;

            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 20;

            var (logs, totalCount) = await _auditLogService.GetFilteredLogsAsync(
                keyword, module, actionFilter, status, logLevel, employee, startDate, endDate, ipAddress: null, browser: null, device: null, page, pageSize);

            var stats = await _auditLogService.GetLogStatsAsync();

            int calcPages = (int)((totalCount + pageSize - 1) / pageSize);
            int totalPages = calcPages < 1 ? 1 : calcPages;

            ViewBag.Keyword = keyword;
            ViewBag.Module = module;
            ViewBag.Action = actionFilter;
            ViewBag.Status = status;
            ViewBag.LogLevel = logLevel;
            ViewBag.Employee = employee;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.Stats = stats;

            return View(logs);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var (logs, _) = await _auditLogService.GetFilteredLogsAsync(
                keyword: id, module: null, action: null, status: null, logLevel: null,
                employee: null, startDate: null, endDate: null, ipAddress: null, browser: null, device: null, page: 1, pageSize: 1);

            var log = logs.FirstOrDefault();
            if (log == null)
            {
                var recentLogs = await _auditLogService.GetRecentActivityAsync(50);
                log = recentLogs.FirstOrDefault(l => l.Id == id);
            }

            if (log == null)
            {
                return Json(new { success = false, message = "Audit log record not found." });
            }

            return Json(new
            {
                success = true,
                id = log.Id,
                employeeName = !string.IsNullOrWhiteSpace(log.EmployeeName) ? log.EmployeeName : (!string.IsNullOrWhiteSpace(log.ExecutedBy) ? log.ExecutedBy : "System"),
                username = !string.IsNullOrWhiteSpace(log.Username) ? log.Username : "system",
                userRole = !string.IsNullOrWhiteSpace(log.UserRole) ? log.UserRole : "Staff",
                employeeId = !string.IsNullOrWhiteSpace(log.EmployeeId) ? log.EmployeeId : "EMP-0000",
                timeIst = log.TimeIstString,
                executionTimeMs = log.ExecutionTimeMs,
                module = log.Module,
                action = log.Action,
                status = log.Status,
                logLevel = log.LogLevel,
                target = log.Target,
                details = log.Details,
                ipAddress = log.IpAddress,
                browser = log.Browser,
                operatingSystem = log.OperatingSystem,
                deviceType = log.DeviceType,
                httpMethod = log.HttpMethod,
                requestUrl = log.RequestUrl,
                previousData = log.PreviousData,
                newData = log.NewData
            });
        }

        [HttpGet]
        public async Task<IActionResult> PollLive(string? lastLogId)
        {
            var stats = await _auditLogService.GetLogStatsAsync();
            var recentLogs = await _auditLogService.GetRecentActivityAsync(1);
            var latestLog = recentLogs.FirstOrDefault();

            bool hasNew = false;
            if (latestLog != null && !string.IsNullOrWhiteSpace(lastLogId) && latestLog.Id != lastLogId)
            {
                hasNew = true;
            }

            var emp = !string.IsNullOrWhiteSpace(latestLog?.EmployeeName) ? latestLog.EmployeeName : (!string.IsNullOrWhiteSpace(latestLog?.ExecutedBy) ? latestLog.ExecutedBy : (!string.IsNullOrWhiteSpace(latestLog?.Username) ? latestLog.Username : "System"));
            var act = !string.IsNullOrWhiteSpace(latestLog?.Action) ? latestLog.Action : "Activity";
            var mod = !string.IsNullOrWhiteSpace(latestLog?.Module) ? latestLog.Module : "System";

            return Json(new
            {
                hasNew = hasNew,
                latestId = latestLog?.Id ?? "",
                latestEmployee = emp,
                latestAction = act,
                latestModule = mod,
                totalLogs = stats.TotalLogs,
                todayLogs = stats.TodayLogs
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(
            string format = "csv",
            string? keyword = null,
            string? module = null,
            string? status = null,
            string? logLevel = null)
        {
            var (logs, _) = await _auditLogService.GetFilteredLogsAsync(
                keyword, module, null, status, logLevel, null, null, null, null, null, null, 1, 5000);

            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                var pdfBytes = _auditLogService.ExportLogsPdf(logs);
                return File(pdfBytes, "application/pdf", $"System_Activity_Logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
            }
            else
            {
                var csvBytes = _auditLogService.ExportLogsCsv(logs);
                return File(csvBytes, "text/csv", $"System_Activity_Logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Purge(int days = 90)
        {
            var userRoleStr = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isUserAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                string.Equals(userRoleStr, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userRoleStr, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (!isUserAdmin)
            {
                return Json(new { success = false, message = "Permission denied. Only Administrators can purge logs." });
            }

            var deletedCount = await _auditLogService.PurgeLogsOlderThanAsync(days);
            await _auditLogService.LogSecurityEventAsync("Audit Logs Purged", $"Purged {deletedCount:N0} activity log records older than {days} days.", "Warning", "Warning");

            return Json(new { success = true, message = $"Successfully purged {deletedCount:N0} logs older than {days} days." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAll()
        {
            var userRoleStr = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isUserAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                string.Equals(userRoleStr, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userRoleStr, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (!isUserAdmin)
            {
                return Json(new { success = false, message = "Permission denied. Only Administrators can clear logs." });
            }

            var deletedCount = await _auditLogService.ClearAllLogsAsync();
            await _auditLogService.LogSecurityEventAsync("Audit Logs Cleared", $"Super Admin cleared all {deletedCount:N0} system activity logs.", "Warning", "Critical");

            return Json(new { success = true, message = $"All {deletedCount:N0} system activity logs cleared successfully." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var userRoleStr = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isUserAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                string.Equals(userRoleStr, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userRoleStr, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (!isUserAdmin)
            {
                return Json(new { success = false, message = "Permission denied. Only Administrators can delete log records." });
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return Json(new { success = false, message = "Invalid log record ID." });
            }

            var success = await _auditLogService.DeleteLogByIdAsync(id);
            if (success)
            {
                await _auditLogService.LogSecurityEventAsync("Audit Log Deleted", $"Administrator deleted single log record (ID: {id}).", "Warning", "Warning");
            }

            return Json(new { success = success, message = success ? "Log record deleted successfully." : "Failed to delete log record." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromBody] List<string> ids)
        {
            var userRoleStr = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isUserAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                string.Equals(userRoleStr, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userRoleStr, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (!isUserAdmin)
            {
                return Json(new { success = false, message = "Permission denied. Only Administrators can delete log records." });
            }

            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "No log records selected for deletion." });
            }

            var deletedCount = await _auditLogService.DeleteLogsByIdsAsync(ids);
            if (deletedCount > 0)
            {
                await _auditLogService.LogSecurityEventAsync("Audit Logs Bulk Deleted", $"Administrator bulk deleted {deletedCount:N0} selected log records.", "Warning", "Warning");
            }

            return Json(new { success = true, message = $"Successfully deleted {deletedCount:N0} selected log records." });
        }
    }
}
