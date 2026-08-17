using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IRepairService
    {
        Task<IEnumerable<RepairTicket>> GetPagedRepairsAsync(string? search, string? status, int page, int pageSize);
        Task<long> GetFilteredCountAsync(string? search, string? status);
        Task<RepairTicket?> GetRepairByIdAsync(string id);
        Task<(bool Success, string Message, RepairTicket? Ticket)> CreateRepairTicketAsync(RepairTicket ticket, string executedBy);
        Task<bool> UpdateRepairStatusAsync(string ticketId, string status, string? technicianName, decimal finalCost, string notes, string executedBy);
    }
}
