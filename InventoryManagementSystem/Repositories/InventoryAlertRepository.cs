using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class InventoryAlertRepository : IInventoryAlertRepository
    {
        private readonly IMongoCollection<InventoryAlertSettings> _settingsCollection;
        private readonly IMongoCollection<InventoryEmailLog> _logsCollection;

        public InventoryAlertRepository(MongoDbContext context)
        {
            _settingsCollection = context.GetCollection<InventoryAlertSettings>("InventoryAlertSettings");
            _logsCollection = context.GetCollection<InventoryEmailLog>("InventoryEmailLogs");
        }

        public async Task<InventoryAlertSettings> GetSettingsAsync()
        {
            var settings = await _settingsCollection.Find(FilterDefinition<InventoryAlertSettings>.Empty).FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new InventoryAlertSettings
                {
                    AdminEmail = "admin@sims.com",
                    LowStockThreshold = 5,
                    EnableLowStockAlerts = true,
                    EnableOutOfStockAlerts = true,
                    EnableStockRestoredAlerts = true,
                    NotificationFrequency = "Immediate",
                    AlertRecipients = new List<string> { "admin@sims.com" },
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "System Default"
                };
                await _settingsCollection.InsertOneAsync(settings);
            }
            return settings;
        }

        public async Task SaveSettingsAsync(InventoryAlertSettings settings)
        {
            settings.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(settings.Id))
            {
                await _settingsCollection.InsertOneAsync(settings);
            }
            else
            {
                await _settingsCollection.ReplaceOneAsync(x => x.Id == settings.Id, settings);
            }
        }

        public async Task CreateEmailLogAsync(InventoryEmailLog log)
        {
            log.SentAt = DateTime.UtcNow;
            await _logsCollection.InsertOneAsync(log);
        }

        public async Task UpdateEmailLogAsync(InventoryEmailLog log)
        {
            await _logsCollection.ReplaceOneAsync(x => x.Id == log.Id, log);
        }

        public async Task<InventoryEmailLog?> GetEmailLogByIdAsync(string id)
        {
            return await _logsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<(IEnumerable<InventoryEmailLog> Items, long TotalCount)> GetFilteredLogsAsync(
            string? keyword,
            string? alertType,
            string? status,
            int page = 1,
            int pageSize = 20)
        {
            var builder = Builders<InventoryEmailLog>.Filter;
            var filters = new List<FilterDefinition<InventoryEmailLog>>();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var regex = new MongoDB.Bson.BsonRegularExpression(keyword, "i");
                filters.Add(builder.Or(
                    builder.Regex(x => x.ProductName, regex),
                    builder.Regex(x => x.Sku, regex),
                    builder.Regex(x => x.RecipientEmail, regex),
                    builder.Regex(x => x.Subject, regex),
                    builder.Regex(x => x.CategoryName, regex)
                ));
            }

            if (!string.IsNullOrWhiteSpace(alertType))
            {
                filters.Add(builder.Eq(x => x.AlertType, alertType));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                filters.Add(builder.Eq(x => x.Status, status));
            }

            var filter = filters.Count > 0 ? builder.And(filters) : builder.Empty;

            var totalCount = await _logsCollection.CountDocumentsAsync(filter);
            var items = await _logsCollection.Find(filter)
                .SortByDescending(x => x.SentAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<InventoryAlertDashboardStats> GetDashboardStatsAsync()
        {
            var totalSent = await _logsCollection.CountDocumentsAsync(Builders<InventoryEmailLog>.Filter.Eq(x => x.Status, "Sent"));
            var failedEmails = await _logsCollection.CountDocumentsAsync(Builders<InventoryEmailLog>.Filter.Eq(x => x.Status, "Failed"));
            var pendingEmails = await _logsCollection.CountDocumentsAsync(Builders<InventoryEmailLog>.Filter.Eq(x => x.Status, "Pending"));

            var todayStart = DateTime.UtcNow.Date;
            var todayAlerts = await _logsCollection.CountDocumentsAsync(Builders<InventoryEmailLog>.Filter.Gte(x => x.SentAt, todayStart));

            var lastAlert = await _logsCollection.Find(FilterDefinition<InventoryEmailLog>.Empty)
                .SortByDescending(x => x.SentAt)
                .FirstOrDefaultAsync();

            var lastSuccessful = await _logsCollection.Find(Builders<InventoryEmailLog>.Filter.Eq(x => x.Status, "Sent"))
                .SortByDescending(x => x.SentAt)
                .FirstOrDefaultAsync();

            return new InventoryAlertDashboardStats
            {
                TotalSent = totalSent,
                TodayAlerts = todayAlerts,
                FailedEmails = failedEmails,
                PendingEmails = pendingEmails,
                LastAlert = lastAlert,
                LastSuccessfulEmail = lastSuccessful
            };
        }
    }
}
