using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Data;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Controllers
{
    [Authorize(Roles = Role.Admin)]
    public class SettingsController : Controller
    {
        private readonly MongoDbContext _context;
        private readonly IAuditLogService _auditLogService;
        private readonly IMongoCollection<Settings> _settingsCollection;

        public SettingsController(MongoDbContext context, IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
            _settingsCollection = _context.GetCollection<Settings>("Settings");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _settingsCollection.Find(FilterDefinition<Settings>.Empty).FirstOrDefaultAsync();
            if (settings == null)
            {
                // Seed default settings if empty
                settings = new Settings
                {
                    CompanyName = "SIMS Enterprise Ltd.",
                    CompanyEmail = "support@sims.com",
                    CompanyPhone = "+91 98765 43210",
                    Address = "123 Business Hub, Mumbai, India",
                    CurrencySymbol = "₹",
                    GstRate = 18.0m,
                    LowStockThreshold = 5
                };
                await _settingsCollection.InsertOneAsync(settings);
            }
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Settings model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var filter = Builders<Settings>.Filter.Eq(s => s.Id, model.Id);
            var update = Builders<Settings>.Update
                .Set(s => s.CompanyName, model.CompanyName)
                .Set(s => s.CompanyEmail, model.CompanyEmail)
                .Set(s => s.CompanyPhone, model.CompanyPhone)
                .Set(s => s.Address, model.Address)
                .Set(s => s.CurrencySymbol, model.CurrencySymbol)
                .Set(s => s.GstRate, model.GstRate)
                .Set(s => s.LowStockThreshold, model.LowStockThreshold);

            await _settingsCollection.UpdateOneAsync(filter, update);
            await _auditLogService.LogActivityAsync("Settings Updated", User.Identity?.Name ?? "Admin", "System Config", "Updated global system configurations.");

            TempData["ToastMessage"] = "Global configurations saved successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }
    }
}
