using InventoryManagementSystem.Helpers;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class RepairService : IRepairService
    {
        private readonly IRepairRepository _repairRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IAuditLogService _auditLogService;
        private readonly IBrevoEmailService _emailService;
        private readonly ILogger<RepairService> _logger;

        public RepairService(
            IRepairRepository repairRepository,
            IDeviceRepository deviceRepository,
            IAuditLogService auditLogService,
            IBrevoEmailService emailService,
            ILogger<RepairService> logger)
        {
            _repairRepository = repairRepository;
            _deviceRepository = deviceRepository;
            _auditLogService = auditLogService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<IEnumerable<RepairTicket>> GetPagedRepairsAsync(string? search, string? status, int page, int pageSize)
        {
            return await _repairRepository.GetPagedRepairsAsync(search, status, page, pageSize);
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? status)
        {
            return await _repairRepository.GetFilteredCountAsync(search, status);
        }

        public async Task<RepairTicket?> GetRepairByIdAsync(string id)
        {
            return await _repairRepository.GetByIdAsync(id);
        }

        public async Task<(bool Success, string Message, RepairTicket? Ticket)> CreateRepairTicketAsync(RepairTicket ticket, string executedBy)
        {
            if (ticket == null) return (false, "Ticket data missing.", null);
            if (string.IsNullOrWhiteSpace(ticket.CustomerName) || string.IsNullOrWhiteSpace(ticket.CustomerPhone))
            {
                return (false, "Customer Name and Phone are required.", null);
            }
            if (string.IsNullOrWhiteSpace(ticket.ProblemDescription))
            {
                return (false, "Problem description is required.", null);
            }

            // Contact & IMEI Validation
            if (!ValidationHelper.IsValidPhone(ticket.CustomerPhone))
            {
                return (false, "Invalid Customer Contact Number format. Phone number must be 10 numeric digits.", null);
            }
            ticket.CustomerEmail = ticket.CustomerEmail?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(ticket.CustomerEmail) && !ValidationHelper.IsValidEmail(ticket.CustomerEmail))
            {
                return (false, "Invalid Customer Email format. Example: customer@example.com", null);
            }
            if (!string.IsNullOrWhiteSpace(ticket.IMEI) && !ValidationHelper.IsValidImei(ticket.IMEI))
            {
                return (false, "Invalid Device IMEI format. IMEI must be a 14 to 16 digit number.", null);
            }

            ticket.TicketNumber = $"REP-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            ticket.CreatedBy = executedBy;
            ticket.CreatedDate = DateTime.UtcNow;
            ticket.Status = "Received";

            await _repairRepository.CreateAsync(ticket);

            // If IMEI provided, update device status if tracked
            if (!string.IsNullOrWhiteSpace(ticket.IMEI))
            {
                var dev = await _deviceRepository.GetByImeiAsync(ticket.IMEI);
                if (dev != null)
                {
                    await _deviceRepository.UpdateStatusAsync(dev.Id, "UnderRepair");
                }
            }

            await _auditLogService.LogActivityAsync(
                "Repair Ticket Created",
                executedBy,
                ticket.TicketNumber,
                $"Created repair ticket #{ticket.TicketNumber} for device '{ticket.DeviceBrand} {ticket.DeviceModel}' (Customer: {ticket.CustomerName})");

            // Send Email Notification to Customer
            await SendNewTicketEmailAsync(ticket);

            return (true, $"Repair ticket #{ticket.TicketNumber} created.", ticket);
        }

        public async Task<bool> UpdateRepairStatusAsync(string ticketId, string status, string? technicianName, decimal finalCost, string notes, string executedBy)
        {
            var ticket = await _repairRepository.GetByIdAsync(ticketId);
            if (ticket == null) return false;

            var oldStatus = ticket.Status;
            ticket.Status = status;
            if (!string.IsNullOrWhiteSpace(technicianName)) ticket.TechnicianName = technicianName;
            if (finalCost > 0) ticket.FinalCost = finalCost;
            if (!string.IsNullOrWhiteSpace(notes)) ticket.Notes = notes;

            if (status == "Delivered" || status == "Ready")
            {
                ticket.CompletedDate = DateTime.UtcNow;

                // Update device status back to InStock or Sold if tracked
                if (!string.IsNullOrWhiteSpace(ticket.IMEI))
                {
                    var dev = await _deviceRepository.GetByImeiAsync(ticket.IMEI);
                    if (dev != null && dev.Status == "UnderRepair")
                    {
                        await _deviceRepository.UpdateStatusAsync(dev.Id, "InStock");
                    }
                }
            }

            await _repairRepository.UpdateAsync(ticket.Id, ticket);

            await _auditLogService.LogActivityAsync(
                "Repair Ticket Updated",
                executedBy,
                ticket.TicketNumber,
                $"Updated repair ticket #{ticket.TicketNumber} status to '{status}' (Technician: {ticket.TechnicianName})");

            // Send Email Notification to Customer on Status Change
            if (!string.Equals(oldStatus, status, StringComparison.OrdinalIgnoreCase))
            {
                await SendStatusUpdateEmailAsync(ticket, oldStatus, status, notes);
            }

            return true;
        }

        public async Task<(bool Success, string Message, RepairTicket? Ticket)> UpdateRepairTicketAsync(RepairTicket ticket, string updatedBy)
        {
            try
            {
                if (ticket == null || string.IsNullOrWhiteSpace(ticket.Id)) return (false, "Invalid ticket ID.", null);

                var existing = await _repairRepository.GetByIdAsync(ticket.Id);
                if (existing == null) return (false, "Repair ticket not found.", null);

                if (!ValidationHelper.IsValidPhone(ticket.CustomerPhone))
                {
                    return (false, "Invalid Customer Phone format.", null);
                }

                if (!string.IsNullOrWhiteSpace(ticket.CustomerEmail) && !ValidationHelper.IsValidEmail(ticket.CustomerEmail))
                {
                    return (false, "Invalid Customer Email format.", null);
                }

                var oldStatus = existing.Status;

                existing.CustomerName = ticket.CustomerName ?? existing.CustomerName;
                existing.CustomerPhone = ticket.CustomerPhone ?? existing.CustomerPhone;
                existing.CustomerEmail = ticket.CustomerEmail?.Trim() ?? string.Empty;
                existing.DeviceBrand = ticket.DeviceBrand ?? existing.DeviceBrand;
                existing.DeviceModel = ticket.DeviceModel ?? existing.DeviceModel;
                existing.ProblemDescription = ticket.ProblemDescription ?? existing.ProblemDescription;
                existing.DeviceCondition = ticket.DeviceCondition ?? existing.DeviceCondition;
                existing.EstimatedCost = ticket.EstimatedCost;
                existing.FinalCost = ticket.FinalCost;
                existing.AdvancePaid = ticket.AdvancePaid;
                existing.TechnicianName = ticket.TechnicianName ?? existing.TechnicianName;
                existing.Notes = ticket.Notes ?? existing.Notes;
                existing.Status = ticket.Status ?? existing.Status;

                await _repairRepository.UpdateAsync(existing.Id, existing);
                await _auditLogService.LogActivityAsync("REPAIR_UPDATED", updatedBy, existing.TicketNumber, $"Updated repair ticket #{existing.TicketNumber}.");

                if (!string.Equals(oldStatus, existing.Status, StringComparison.OrdinalIgnoreCase))
                {
                    await SendStatusUpdateEmailAsync(existing, oldStatus, existing.Status, existing.Notes);
                }

                return (true, $"Repair ticket #{existing.TicketNumber} updated successfully.", existing);
            }
            catch (Exception ex)
            {
                return (false, "Error updating repair ticket: " + ex.Message, null);
            }
        }

        public async Task<(bool Success, string Message)> DeleteRepairTicketAsync(string id, string deletedBy)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return (false, "Invalid ticket ID.");

                var existing = await _repairRepository.GetByIdAsync(id);
                if (existing == null) return (false, "Repair ticket not found.");

                await _repairRepository.DeleteAsync(id);
                await _auditLogService.LogActivityAsync("REPAIR_DELETED", deletedBy, existing.TicketNumber, $"Deleted repair ticket #{existing.TicketNumber}.");
                return (true, $"Repair ticket #{existing.TicketNumber} deleted successfully.");
            }
            catch (Exception ex)
            {
                return (false, "Error deleting repair ticket: " + ex.Message);
            }
        }

        private async Task SendNewTicketEmailAsync(RepairTicket ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket.CustomerEmail) || !ValidationHelper.IsValidEmail(ticket.CustomerEmail))
            {
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html><head><meta charset='utf-8'/><style>");
                sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; margin: 0; padding: 16px; font-size: 13px; }");
                sb.AppendLine(".card { max-width: 560px; width: 100%; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); box-sizing: border-box; }");
                sb.AppendLine(".header { background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); color: #ffffff; padding: 22px 20px; text-align: center; }");
                sb.AppendLine(".badge { display: inline-block; padding: 5px 12px; border-radius: 20px; font-weight: 700; font-size: 12px; letter-spacing: 0.5px; }");
                sb.AppendLine(".badge-received { background: #e0f2fe; color: #0284c7; }");
                sb.AppendLine(".content { padding: 22px 20px; }");
                sb.AppendLine(".ticket-box { background: #f1f5f9; border-left: 4px solid #0284c7; padding: 12px 16px; border-radius: 6px; margin-bottom: 20px; }");
                sb.AppendLine(".table-detail { width: 100%; border-collapse: collapse; table-layout: fixed; margin-bottom: 20px; word-break: break-word; overflow-wrap: break-word; }");
                sb.AppendLine(".table-detail td { padding: 7px 10px; border-bottom: 1px solid #f1f5f9; font-size: 12.5px; line-height: 1.45; vertical-align: top; word-break: break-word; overflow-wrap: break-word; }");
                sb.AppendLine(".table-detail td.label { color: #64748b; font-weight: 600; width: 38%; font-size: 12px; }");
                sb.AppendLine(".table-detail td.val { color: #0f172a; font-weight: 600; width: 62%; font-size: 12.5px; }");
                sb.AppendLine(".cost-box { background: #ecfdf5; border: 1px solid #a7f3d0; border-radius: 8px; padding: 14px 16px; margin-bottom: 20px; text-align: center; }");
                sb.AppendLine(".cost-box table { width: 100%; table-layout: fixed; border-collapse: collapse; font-size: 12.5px; }");
                sb.AppendLine(".footer { background: #f8fafc; padding: 16px 20px; text-align: center; font-size: 11.5px; color: #94a3b8; border-top: 1px solid #e2e8f0; }");
                sb.AppendLine("</style></head><body>");
                sb.AppendLine("<div class='card'>");
                sb.AppendLine("<div class='header'>");
                sb.AppendLine("<h2 style='margin:0 0 4px 0; font-size: 20px; color: #ffffff;'>SIMS Service & Repair</h2>");
                sb.AppendLine("<p style='margin:0; opacity: 0.85; font-size: 13px; color: #cbd5e1;'>Device Repair Ticket Confirmation</p>");
                sb.AppendLine("</div>");
                sb.AppendLine("<div class='content'>");
                sb.AppendLine($"<p style='font-size: 14px; margin-top: 0;'>Hello <strong>{System.Net.WebUtility.HtmlEncode(ticket.CustomerName)}</strong>,</p>");
                sb.AppendLine("<p style='font-size: 13px; color: #475569; line-height: 1.45;'>Your device repair request has been successfully registered. Our technical team will inspect your device and keep you updated on its progress.</p>");

                sb.AppendLine("<div class='ticket-box'>");
                sb.AppendLine("<div style='font-size: 11.5px; color: #64748b; text-transform: uppercase; font-weight: 700; margin-bottom: 2px;'>Job Ticket Number</div>");
                sb.AppendLine($"<div style='font-size: 18px; font-weight: 800; font-family: monospace; color: #0f172a;'>#{ticket.TicketNumber}</div>");
                sb.AppendLine($"<div style='margin-top: 6px;'><span class='badge badge-received'>Status: {ticket.Status}</span></div>");
                sb.AppendLine("</div>");

                sb.AppendLine("<table class='table-detail'>");
                sb.AppendLine($"<tr><td class='label'>Device Brand & Model</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.DeviceBrand)} {System.Net.WebUtility.HtmlEncode(ticket.DeviceModel)}</td></tr>");
                if (!string.IsNullOrWhiteSpace(ticket.IMEI))
                {
                    sb.AppendLine($"<tr><td class='label'>IMEI Number</td><td class='val' style='font-family:monospace; font-size: 12px;'>{System.Net.WebUtility.HtmlEncode(ticket.IMEI)}</td></tr>");
                }
                sb.AppendLine($"<tr><td class='label'>Problem Reported</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.ProblemDescription)}</td></tr>");
                if (!string.IsNullOrWhiteSpace(ticket.DeviceCondition))
                {
                    sb.AppendLine($"<tr><td class='label'>Device Condition</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.DeviceCondition)}</td></tr>");
                }
                if (!string.IsNullOrWhiteSpace(ticket.TechnicianName))
                {
                    sb.AppendLine($"<tr><td class='label'>Assigned Technician</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.TechnicianName)}</td></tr>");
                }
                sb.AppendLine($"<tr><td class='label'>Customer Contact</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.CustomerPhone)}</td></tr>");
                sb.AppendLine($"<tr><td class='label'>Customer Email</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.CustomerEmail)}</td></tr>");
                sb.AppendLine($"<tr><td class='label'>Advance Payment Paid</td><td class='val' style='color:#0284c7; font-weight:700;'>₹{ticket.AdvancePaid:N2}</td></tr>");
                sb.AppendLine($"<tr><td class='label'>Date Logged</td><td class='val'>{ticket.CreatedDate:dd-MMM-yyyy hh:mm tt} IST</td></tr>");
                sb.AppendLine("</table>");

                sb.AppendLine("<div class='cost-box'>");
                sb.AppendLine("<table>");
                sb.AppendLine($"<tr><td style='color:#065f46; font-weight:600; padding:3px 0; font-size:12px;'>Estimated Total Cost:</td><td style='text-align:right; font-weight:800; font-size:14px; color:#047857;'>₹{ticket.EstimatedCost:N2}</td></tr>");
                sb.AppendLine($"<tr><td style='color:#0369a1; font-weight:600; padding:3px 0; font-size:12px;'>Advance Payment Received:</td><td style='text-align:right; font-weight:800; font-size:14px; color:#0284c7;'>₹{ticket.AdvancePaid:N2}</td></tr>");
                var estBalance = ticket.EstimatedCost > ticket.AdvancePaid ? ticket.EstimatedCost - ticket.AdvancePaid : 0;
                sb.AppendLine($"<tr style='border-top: 1px dashed #a7f3d0;'><td style='color:#475569; font-weight:600; padding:5px 0 0 0; font-size:12px;'>Estimated Balance Due:</td><td style='text-align:right; font-weight:800; font-size:14px; color:#0f172a; padding:5px 0 0 0;'>₹{estBalance:N2}</td></tr>");
                sb.AppendLine("</table>");
                sb.AppendLine("</div>");

                sb.AppendLine("<div style='background: #f8fafc; border-radius: 8px; padding: 12px 14px; font-size: 12px; color: #64748b; line-height: 1.45;'>");
                sb.AppendLine("<strong>What's Next?</strong><br/>");
                sb.AppendLine("• Our technicians are diagnosing your device.<br/>");
                sb.AppendLine("• You will receive automated email updates whenever the job status changes.<br/>");
                sb.AppendLine($"• Please quote Ticket <strong>#{ticket.TicketNumber}</strong> whenever inquiring at our store.");
                sb.AppendLine("</div>");

                sb.AppendLine("</div>");
                sb.AppendLine("<div class='footer'>");
                sb.AppendLine("<p style='margin:0 0 4px 0;'>Smart Inventory & Service Management System (SIMS)</p>");
                sb.AppendLine("<p style='margin:0;'>Automated Notification — Please do not reply directly to this email.</p>");
                sb.AppendLine("</div>");
                sb.AppendLine("</div></body></html>");

                var (success, messageId, apiResp, errorMsg) = await _emailService.SendTransactionalEmailAsync(
                    ticket.CustomerEmail.Trim(),
                    $"[SIMS] Repair Ticket Created: #{ticket.TicketNumber} - {ticket.DeviceBrand} {ticket.DeviceModel}",
                    sb.ToString()
                );

                if (success)
                {
                    _logger.LogInformation("Repair creation email sent successfully to {Email} for Ticket #{TicketNumber}", ticket.CustomerEmail, ticket.TicketNumber);
                    await _auditLogService.LogActivityAsync("REPAIR_EMAIL_SENT", "System", ticket.TicketNumber, $"Sent ticket confirmation email to {ticket.CustomerEmail}");
                }
                else
                {
                    _logger.LogWarning("Failed to send repair creation email to {Email}: {Error}", ticket.CustomerEmail, errorMsg);
                    await _auditLogService.LogActivityAsync("REPAIR_EMAIL_FAILED", "System", ticket.TicketNumber, $"Failed to send ticket confirmation email to {ticket.CustomerEmail}: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception sending repair creation email for Ticket #{TicketNumber}", ticket.TicketNumber);
            }
        }

        private async Task SendStatusUpdateEmailAsync(RepairTicket ticket, string oldStatus, string newStatus, string? technicianNotes)
        {
            if (string.IsNullOrWhiteSpace(ticket.CustomerEmail) || !ValidationHelper.IsValidEmail(ticket.CustomerEmail))
            {
                return;
            }

            try
            {
                var (statusBg, statusColor, statusMessage) = newStatus switch
                {
                    "Diagnosing" => ("#e0f2fe", "#0284c7", "Our technician is currently diagnosing the reported issue with your device."),
                    "Repairing" => ("#fef3c7", "#b45309", "Your device is actively being repaired by our technician."),
                    "Ready" => ("#dcfce7", "#15803d", "Great news! Your device has been repaired and is ready for pickup at our store!"),
                    "Delivered" => ("#ecfdf5", "#047857", "Your device has been delivered and service closed. Thank you for your business!"),
                    "Cancelled" => ("#fee2e2", "#b91c1c", "Your repair ticket has been cancelled. Please contact our store for assistance."),
                    _ => ("#f1f5f9", "#475569", $"Your repair ticket status has been updated to '{newStatus}'.")
                };

                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html><head><meta charset='utf-8'/><style>");
                sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; margin: 0; padding: 16px; font-size: 13px; }");
                sb.AppendLine(".card { max-width: 560px; width: 100%; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); box-sizing: border-box; }");
                sb.AppendLine(".header { background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); color: #ffffff; padding: 22px 20px; text-align: center; }");
                sb.AppendLine(".badge { display: inline-block; padding: 5px 14px; border-radius: 20px; font-weight: 700; font-size: 13px; letter-spacing: 0.5px; }");
                sb.AppendLine(".content { padding: 22px 20px; }");
                sb.AppendLine(".status-banner { padding: 14px; border-radius: 8px; margin-bottom: 20px; text-align: center; }");
                sb.AppendLine(".table-detail { width: 100%; border-collapse: collapse; table-layout: fixed; margin-bottom: 20px; word-break: break-word; overflow-wrap: break-word; }");
                sb.AppendLine(".table-detail td { padding: 7px 10px; border-bottom: 1px solid #f1f5f9; font-size: 12.5px; line-height: 1.45; vertical-align: top; word-break: break-word; overflow-wrap: break-word; }");
                sb.AppendLine(".table-detail td.label { color: #64748b; font-weight: 600; width: 38%; font-size: 12px; }");
                sb.AppendLine(".table-detail td.val { color: #0f172a; font-weight: 600; width: 62%; font-size: 12.5px; }");
                sb.AppendLine(".notes-box { background: #fefce8; border: 1px solid #fde047; border-radius: 8px; padding: 12px 14px; margin-bottom: 20px; word-break: break-word; overflow-wrap: break-word; }");
                sb.AppendLine(".cost-box { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 14px 16px; margin-bottom: 20px; }");
                sb.AppendLine(".cost-box table { width: 100%; table-layout: fixed; border-collapse: collapse; font-size: 12.5px; }");
                sb.AppendLine(".footer { background: #f8fafc; padding: 16px 20px; text-align: center; font-size: 11.5px; color: #94a3b8; border-top: 1px solid #e2e8f0; }");
                sb.AppendLine("</style></head><body>");
                sb.AppendLine("<div class='card'>");
                sb.AppendLine("<div class='header'>");
                sb.AppendLine("<h2 style='margin:0 0 4px 0; font-size: 20px; color: #ffffff;'>SIMS Service & Repair</h2>");
                sb.AppendLine("<p style='margin:0; opacity: 0.85; font-size: 13px; color: #cbd5e1;'>Repair Ticket Status Update</p>");
                sb.AppendLine("</div>");
                sb.AppendLine("<div class='content'>");
                sb.AppendLine($"<p style='font-size: 14px; margin-top: 0;'>Hello <strong>{System.Net.WebUtility.HtmlEncode(ticket.CustomerName)}</strong>,</p>");

                sb.AppendLine($"<div class='status-banner' style='background: {statusBg}; border: 1px solid {statusColor};'>");
                sb.AppendLine($"<div style='font-size: 11.5px; color: {statusColor}; text-transform: uppercase; font-weight: 700; margin-bottom: 4px;'>Ticket #{ticket.TicketNumber} Status Update</div>");
                sb.AppendLine($"<div class='badge' style='background: #ffffff; color: {statusColor}; border: 1px solid {statusColor};'>{newStatus}</div>");
                sb.AppendLine($"<p style='margin: 8px 0 0 0; font-size: 13px; color: {statusColor}; font-weight: 600;'>{statusMessage}</p>");
                sb.AppendLine("</div>");

                if (!string.IsNullOrWhiteSpace(technicianNotes) || !string.IsNullOrWhiteSpace(ticket.Notes))
                {
                    var notesToShow = !string.IsNullOrWhiteSpace(technicianNotes) ? technicianNotes : ticket.Notes;
                    sb.AppendLine("<div class='notes-box'>");
                    sb.AppendLine("<div style='font-size: 11.5px; font-weight: 700; color: #854d0e; text-transform: uppercase; margin-bottom: 4px;'>Technician Remarks / Notes:</div>");
                    sb.AppendLine($"<div style='font-size: 12.5px; color: #713f12; line-height: 1.4;'>{System.Net.WebUtility.HtmlEncode(notesToShow)}</div>");
                    sb.AppendLine("</div>");
                }

                sb.AppendLine("<table class='table-detail'>");
                sb.AppendLine($"<tr><td class='label'>Device</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.DeviceBrand)} {System.Net.WebUtility.HtmlEncode(ticket.DeviceModel)}</td></tr>");
                if (!string.IsNullOrWhiteSpace(ticket.IMEI))
                {
                    sb.AppendLine($"<tr><td class='label'>IMEI Number</td><td class='val' style='font-family:monospace; font-size: 12px;'>{System.Net.WebUtility.HtmlEncode(ticket.IMEI)}</td></tr>");
                }
                sb.AppendLine($"<tr><td class='label'>Problem Reported</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.ProblemDescription)}</td></tr>");
                if (!string.IsNullOrWhiteSpace(ticket.TechnicianName))
                {
                    sb.AppendLine($"<tr><td class='label'>Technician</td><td class='val'>{System.Net.WebUtility.HtmlEncode(ticket.TechnicianName)}</td></tr>");
                }
                sb.AppendLine($"<tr><td class='label'>Last Updated</td><td class='val'>{DateTime.UtcNow:dd-MMM-yyyy hh:mm tt} IST</td></tr>");
                sb.AppendLine("</table>");

                sb.AppendLine("<div class='cost-box'>");
                sb.AppendLine("<table>");
                sb.AppendLine($"<tr><td style='color:#64748b; padding:3px 0; font-size:12px;'>Estimated Cost:</td><td style='text-align:right; font-weight:600; font-size:13.5px;'>₹{ticket.EstimatedCost:N2}</td></tr>");
                if (ticket.FinalCost > 0)
                {
                    sb.AppendLine($"<tr><td style='color:#0f172a; font-weight:700; padding:3px 0; font-size:12px;'>Final Service Cost:</td><td style='text-align:right; font-weight:800; font-size:14px; color:#047857;'>₹{ticket.FinalCost:N2}</td></tr>");
                }
                if (ticket.AdvancePaid > 0)
                {
                    sb.AppendLine($"<tr><td style='color:#64748b; padding:3px 0; font-size:12px;'>Advance Paid:</td><td style='text-align:right; font-weight:600; font-size:13.5px; color:#0284c7;'>₹{ticket.AdvancePaid:N2}</td></tr>");
                    var balance = ticket.FinalCost > ticket.AdvancePaid ? ticket.FinalCost - ticket.AdvancePaid : 0;
                    if (balance > 0)
                    {
                        sb.AppendLine($"<tr style='border-top: 1px dashed #cbd5e1;'><td style='color:#b91c1c; font-weight:700; padding:5px 0 0 0; font-size:12px;'>Balance Due at Pickup:</td><td style='text-align:right; font-weight:800; font-size:14px; color:#b91c1c; padding:5px 0 0 0;'>₹{balance:N2}</td></tr>");
                    }
                }
                sb.AppendLine("</table>");
                sb.AppendLine("</div>");

                sb.AppendLine("<div style='background: #f8fafc; border-radius: 8px; padding: 12px 14px; font-size: 12px; color: #64748b; line-height: 1.45;'>");
                if (newStatus == "Ready")
                {
                    sb.AppendLine("<strong>Pickup Information:</strong><br/>");
                    sb.AppendLine("Please bring your ticket confirmation or phone when collecting your device.<br/>");
                }
                sb.AppendLine($"If you have any questions, please contact our support team quoting Ticket <strong>#{ticket.TicketNumber}</strong>.");
                sb.AppendLine("</div>");

                sb.AppendLine("</div>");
                sb.AppendLine("<div class='footer'>");
                sb.AppendLine("<p style='margin:0 0 4px 0;'>Smart Inventory & Service Management System (SIMS)</p>");
                sb.AppendLine("<p style='margin:0;'>Automated Notification — Please do not reply directly to this email.</p>");
                sb.AppendLine("</div>");
                sb.AppendLine("</div></body></html>");

                var (success, messageId, apiResp, errorMsg) = await _emailService.SendTransactionalEmailAsync(
                    ticket.CustomerEmail.Trim(),
                    $"[SIMS] Update on Repair Ticket #{ticket.TicketNumber}: {newStatus}",
                    sb.ToString()
                );

                if (success)
                {
                    _logger.LogInformation("Repair status update email sent successfully to {Email} for Ticket #{TicketNumber} (Status: {Status})", ticket.CustomerEmail, ticket.TicketNumber, newStatus);
                    await _auditLogService.LogActivityAsync("REPAIR_STATUS_EMAIL_SENT", "System", ticket.TicketNumber, $"Sent status update ({newStatus}) email to {ticket.CustomerEmail}");
                }
                else
                {
                    _logger.LogWarning("Failed to send repair status update email to {Email}: {Error}", ticket.CustomerEmail, errorMsg);
                    await _auditLogService.LogActivityAsync("REPAIR_STATUS_EMAIL_FAILED", "System", ticket.TicketNumber, $"Failed to send status update email to {ticket.CustomerEmail}: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception sending status update email for Ticket #{TicketNumber}", ticket.TicketNumber);
            }
        }
    }
}
