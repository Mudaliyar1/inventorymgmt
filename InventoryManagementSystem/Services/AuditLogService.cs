using Microsoft.AspNetCore.Http;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Extensions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
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

            QuestPDF.Settings.License = LicenseType.Community;
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
            await LogExAsync(action, module, target, details, "Success", "Information", previousData, newData);
        }

        public async Task LogExAsync(
            string action,
            string module,
            string target,
            string details,
            string status = "Success",
            string logLevel = "Information",
            string previousData = "",
            string newData = "",
            string referenceId = "",
            long executionTimeMs = 0)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";
            var requestUrl = httpContext?.Request?.Path.Value ?? "/";
            var httpMethod = httpContext?.Request?.Method ?? "GET";

            var (browser, os, device, deviceType) = ParseUserAgent(userAgent);

            string username = httpContext?.User?.Identity?.Name ?? "System";
            string employeeId = "EMP-0000";
            string employeeName = username;
            string userRole = "Staff";

            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var roleClaim = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
                if (!string.IsNullOrEmpty(roleClaim)) userRole = roleClaim;

                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _userRepository.GetByIdAsync(userId);
                    if (user != null)
                    {
                        username = user.Username;
                        employeeId = !string.IsNullOrEmpty(user.EmployeeId) ? user.EmployeeId : $"EMP-{(user.Id.Length > 6 ? user.Id[..6] : user.Id)}";
                        employeeName = user.FullName;
                        userRole = user.Role.ToString();
                    }
                }
            }

            var log = new AuditLog
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                Username = username,
                UserRole = userRole,
                Action = action,
                ExecutedBy = $"{employeeName} ({username})",
                Module = module,
                Target = target,
                PreviousData = previousData,
                NewData = newData,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                Browser = browser,
                OperatingSystem = os,
                Device = device,
                DeviceType = deviceType,
                RequestUrl = requestUrl,
                HttpMethod = httpMethod,
                Details = details,
                ReferenceId = referenceId,
                Status = status,
                LogLevel = logLevel,
                ExecutionTimeMs = executionTimeMs
            };

            await _auditLogRepository.CreateAsync(log);
        }

        public async Task LogSecurityEventAsync(
            string action,
            string details,
            string status = "Failed",
            string logLevel = "Warning")
        {
            await LogExAsync(action, "Security", "Security Firewall", details, status, logLevel);
        }

        public async Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int count)
        {
            return await _auditLogRepository.GetRecentLogsAsync(count);
        }

        public async Task<IEnumerable<AuditLog>> GetLogsByEmployeeAsync(string username, int count = 50)
        {
            var (logs, _) = await _auditLogRepository.GetFilteredLogsAsync(
                keyword: null, module: null, action: null, status: null, logLevel: null,
                employee: username, startDate: null, endDate: null, ipAddress: null, browser: null, device: null,
                page: 1, pageSize: count);

            return logs;
        }

        public async Task<(IEnumerable<AuditLog> Items, long TotalCount)> GetFilteredLogsAsync(
            string? keyword,
            string? module,
            string? action,
            string? status,
            string? logLevel,
            string? employee,
            DateTime? startDate,
            DateTime? endDate,
            string? ipAddress,
            string? browser,
            string? device,
            int page = 1,
            int pageSize = 20)
        {
            return await _auditLogRepository.GetFilteredLogsAsync(
                keyword, module, action, status, logLevel, employee, startDate, endDate, ipAddress, browser, device, page, pageSize);
        }

        public async Task<AuditLogStats> GetLogStatsAsync()
        {
            return await _auditLogRepository.GetLogStatsAsync();
        }

        public async Task<long> PurgeLogsOlderThanAsync(int days)
        {
            return await _auditLogRepository.DeleteLogsOlderThanAsync(days);
        }

        public async Task<long> ClearAllLogsAsync()
        {
            return await _auditLogRepository.ClearAllLogsAsync();
        }

        public byte[] ExportLogsCsv(IEnumerable<AuditLog> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Log ID,Date (IST),Employee,Username,Role,Module,Action,Description,Status,LogLevel,IP Address,Browser,OS,Device,Execution Time (ms)");

            foreach (var log in logs)
            {
                var cleanDesc = (log.Details ?? "").Replace("\"", "\"\"");
                var cleanTarget = (log.Target ?? "").Replace("\"", "\"\"");

                sb.AppendLine($"\"{log.Id}\",\"{log.TimeIstString}\",\"{log.EmployeeName}\",\"{log.Username}\",\"{log.UserRole}\",\"{log.Module}\",\"{log.Action}\",\"{cleanTarget} - {cleanDesc}\",\"{log.Status}\",\"{log.LogLevel}\",\"{log.IpAddress}\",\"{log.Browser}\",\"{log.OperatingSystem}\",\"{log.Device}\",\"{log.ExecutionTimeMs}\"");
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public byte[] ExportLogsPdf(IEnumerable<AuditLog> logs)
        {
            var logList = logs.ToList();
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4.Landscape());
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("SMART INVENTORY MANAGEMENT SYSTEM").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                            col.Item().Text("Enterprise Activity & Audit Log Report").FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
                        });

                        row.ConstantItem(150).Column(col =>
                        {
                            col.Item().Text($"Generated: {DateTime.UtcNow.ToIstString("MMM d, yyyy HH:mm IST")}").FontSize(8).AlignRight();
                            col.Item().Text($"Total Entries: {logList.Count}").FontSize(8).AlignRight().Bold();
                        });
                    });

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(110); // Date
                            columns.ConstantColumn(100); // Employee
                            columns.ConstantColumn(90);  // Module
                            columns.ConstantColumn(90);  // Action
                            columns.RelativeColumn();    // Description
                            columns.ConstantColumn(60);  // Status
                            columns.ConstantColumn(80);  // IP
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Date & Time (IST)").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Employee").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Module").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Action").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Description / Target").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Status").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("IP Address").FontColor(Colors.White).Bold();
                        });

                        int idx = 1;
                        foreach (var l in logList)
                        {
                            var bg = idx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Background(bg).Padding(4).Text(l.TimeIstString).FontSize(8);
                            table.Cell().Background(bg).Padding(4).Text($"{l.EmployeeName}\n({l.Username})").FontSize(8);
                            table.Cell().Background(bg).Padding(4).Text(l.Module).FontSize(8);
                            table.Cell().Background(bg).Padding(4).Text(l.Action).FontSize(8).Bold();
                            table.Cell().Background(bg).Padding(4).Text($"{l.Target} {l.Details}").FontSize(8);
                            table.Cell().Background(bg).Padding(4).Text(l.Status).FontSize(8).Bold();
                            table.Cell().Background(bg).Padding(4).Text(l.IpAddress).FontSize(8);
                            idx++;
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        private static (string Browser, string OS, string Device, string DeviceType) ParseUserAgent(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return ("Unknown Browser", "Unknown OS", "Unknown Device", "Desktop");

            string browser = "Browser";
            if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "Microsoft Edge";
            else if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) browser = "Google Chrome";
            else if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) browser = "Mozilla Firefox";
            else if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) browser = "Apple Safari";
            else if (userAgent.Contains("Trident/", StringComparison.OrdinalIgnoreCase)) browser = "Internet Explorer";

            string os = "Windows OS";
            if (userAgent.Contains("Windows NT 10.0", StringComparison.OrdinalIgnoreCase)) os = "Windows 10/11";
            else if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) os = "Windows";
            else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) os = "Android OS";
            else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) os = "iOS";
            else if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)) os = "macOS";
            else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) os = "Linux";

            string deviceType = "Desktop";
            string device = "Windows Workstation";

            if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            {
                deviceType = "Mobile";
                device = "Mobile Smartphone";
            }
            else if (userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            {
                deviceType = "Tablet";
                device = "Tablet Device";
            }

            return (browser, os, device, deviceType);
        }
    }
}
