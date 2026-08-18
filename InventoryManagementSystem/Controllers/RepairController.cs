using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class RepairController : Controller
    {
        private readonly IRepairService _repairService;

        public RepairController(IRepairService repairService)
        {
            _repairService = repairService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            int pageSize = 20;
            var tickets = await _repairService.GetPagedRepairsAsync(search, status, page, pageSize);
            var totalCount = await _repairService.GetFilteredCountAsync(search, status);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View(tickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] RepairTicket ticket)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message, result) = await _repairService.CreateRepairTicketAsync(ticket, executedBy);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success, message, ticket = result });
            }

            if (success)
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, string status, string? technicianName, decimal finalCost, string notes)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var success = await _repairService.UpdateRepairStatusAsync(id, status, technicianName, finalCost, notes, executedBy);
            var message = success ? "Repair ticket updated successfully." : "Failed to update repair ticket.";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success, message });
            }

            if (success)
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = message;
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Json(new { success = false, message = "Ticket ID is required." });

            var ticket = await _repairService.GetRepairByIdAsync(id.Trim());
            if (ticket == null) return Json(new { success = false, message = "Repair ticket not found." });

            return Json(new {
                success = true,
                id = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                createdDate = ticket.CreatedDate.ToString("yyyy-MM-dd HH:mm IST"),
                completedDate = ticket.CompletedDate?.ToString("yyyy-MM-dd HH:mm IST"),
                customerName = ticket.CustomerName,
                customerPhone = ticket.CustomerPhone,
                deviceBrand = ticket.DeviceBrand,
                deviceModel = ticket.DeviceModel,
                imei = ticket.IMEI,
                problemDescription = ticket.ProblemDescription,
                deviceCondition = ticket.DeviceCondition,
                status = ticket.Status,
                estimatedCost = ticket.EstimatedCost,
                finalCost = ticket.FinalCost,
                advancePaid = ticket.AdvancePaid,
                technicianName = ticket.TechnicianName,
                notes = ticket.Notes,
                createdBy = ticket.CreatedBy
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromForm] RepairTicket ticket)
        {
            var updatedBy = User.Identity?.Name ?? "Admin";
            var (success, message, result) = await _repairService.UpdateRepairTicketAsync(ticket, updatedBy);

            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var deletedBy = User.Identity?.Name ?? "Admin";
            var (success, message) = await _repairService.DeleteRepairTicketAsync(id, deletedBy);

            TempData["ToastMessage"] = message;
            TempData["ToastType"] = success ? "success" : "danger";
            return RedirectToAction("Index");
        }
    }
}
