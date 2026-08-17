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

            if (success)
            {
                TempData["ToastMessage"] = "Repair ticket updated successfully.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = "Failed to update repair ticket.";
                TempData["ToastType"] = "danger";
            }

            return RedirectToAction("Index");
        }
    }
}
