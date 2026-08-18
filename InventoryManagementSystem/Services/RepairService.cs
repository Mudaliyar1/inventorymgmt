using InventoryManagementSystem.Helpers;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class RepairService : IRepairService
    {
        private readonly IRepairRepository _repairRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IAuditLogService _auditLogService;

        public RepairService(
            IRepairRepository repairRepository,
            IDeviceRepository deviceRepository,
            IAuditLogService auditLogService)
        {
            _repairRepository = repairRepository;
            _deviceRepository = deviceRepository;
            _auditLogService = auditLogService;
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

            return (true, $"Repair ticket #{ticket.TicketNumber} created.", ticket);
        }

        public async Task<bool> UpdateRepairStatusAsync(string ticketId, string status, string? technicianName, decimal finalCost, string notes, string executedBy)
        {
            var ticket = await _repairRepository.GetByIdAsync(ticketId);
            if (ticket == null) return false;

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

                existing.CustomerName = ticket.CustomerName ?? existing.CustomerName;
                existing.CustomerPhone = ticket.CustomerPhone ?? existing.CustomerPhone;
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
    }
}
