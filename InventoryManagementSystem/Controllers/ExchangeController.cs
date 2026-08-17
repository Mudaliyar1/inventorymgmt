using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize]
    public class ExchangeController : Controller
    {
        private readonly IExchangeService _exchangeService;

        public ExchangeController(IExchangeService exchangeService)
        {
            _exchangeService = exchangeService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            int pageSize = 20;
            var exchanges = await _exchangeService.GetPagedExchangesAsync(search, page, pageSize);
            var totalCount = await _exchangeService.GetFilteredCountAsync(search);

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View(exchanges);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessExchange([FromForm] ExchangeRecord exchangeRecord)
        {
            var executedBy = User.Identity?.Name ?? "Admin";
            var (success, message, result) = await _exchangeService.ProcessExchangeAsync(exchangeRecord, executedBy);

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
    }
}
