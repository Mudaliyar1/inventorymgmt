using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class AuditLogRepository : BaseRepository<AuditLog>, IAuditLogRepository
    {
        public AuditLogRepository(MongoDbContext context, ITenantContext tenantContext) : base(context, "AuditLogs", tenantContext)
        {
            Task.Run(async () => await EnsureIndexesCreatedAsync());
        }

        public async Task EnsureIndexesCreatedAsync()
        {
            try
            {
                var indexBuilder = Builders<AuditLog>.IndexKeys;

                var index1 = new CreateIndexModel<AuditLog>(indexBuilder.Descending(x => x.Timestamp));
                var index2 = new CreateIndexModel<AuditLog>(indexBuilder.Ascending(x => x.Module));
                var index3 = new CreateIndexModel<AuditLog>(indexBuilder.Ascending(x => x.Action));
                var index4 = new CreateIndexModel<AuditLog>(indexBuilder.Ascending(x => x.Status));
                var index5 = new CreateIndexModel<AuditLog>(indexBuilder.Ascending(x => x.LogLevel));
                var index6 = new CreateIndexModel<AuditLog>(indexBuilder.Ascending(x => x.Username));
                var index7 = new CreateIndexModel<AuditLog>(indexBuilder.Ascending(x => x.EmployeeId));

                await _collection.Indexes.CreateManyAsync(new[] { index1, index2, index3, index4, index5, index6, index7 });
            }
            catch
            {
                // Ignore index creation errors if already present
            }
        }

        public async Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count)
        {
            return await _collection.Find(GetTenantFilter())
                .SortByDescending(l => l.Timestamp)
                .Limit(count)
                .ToListAsync();
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
            try
            {
                var builder = Builders<AuditLog>.Filter;
                var filters = new List<FilterDefinition<AuditLog>>();

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var k = keyword.Trim();
                    filters.Add(builder.Or(
                        builder.Regex(x => x.EmployeeName, new MongoDB.Bson.BsonRegularExpression(k, "i")),
                        builder.Regex(x => x.Username, new MongoDB.Bson.BsonRegularExpression(k, "i")),
                        builder.Regex(x => x.EmployeeId, new MongoDB.Bson.BsonRegularExpression(k, "i")),
                        builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression(k, "i")),
                        builder.Regex(x => x.Module, new MongoDB.Bson.BsonRegularExpression(k, "i")),
                        builder.Regex(x => x.Target, new MongoDB.Bson.BsonRegularExpression(k, "i")),
                        builder.Regex(x => x.Details, new MongoDB.Bson.BsonRegularExpression(k, "i")),
                        builder.Regex(x => x.IpAddress, new MongoDB.Bson.BsonRegularExpression(k, "i")),
                        builder.Regex(x => x.ReferenceId, new MongoDB.Bson.BsonRegularExpression(k, "i"))
                    ));
                }

                if (!string.IsNullOrWhiteSpace(module))
                {
                    var m = module.Trim();
                    var modSearch = GetModuleSearchPattern(m);
                    filters.Add(builder.Regex(x => x.Module, new MongoDB.Bson.BsonRegularExpression(modSearch, "i")));
                }

                if (!string.IsNullOrWhiteSpace(action))
                {
                    var a = action.Trim();
                    filters.Add(builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression(a, "i")));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    var st = status.Trim();
                    filters.Add(builder.Or(
                        builder.Eq(x => x.Status, st),
                        builder.Eq(x => x.LogLevel, st),
                        builder.Regex(x => x.Status, new MongoDB.Bson.BsonRegularExpression(st, "i"))
                    ));
                }

                if (!string.IsNullOrWhiteSpace(logLevel))
                {
                    var ll = logLevel.Trim();
                    filters.Add(builder.Or(
                        builder.Eq(x => x.LogLevel, ll),
                        builder.Eq(x => x.Status, ll),
                        builder.Regex(x => x.LogLevel, new MongoDB.Bson.BsonRegularExpression(ll, "i"))
                    ));
                }

                if (!string.IsNullOrWhiteSpace(employee))
                {
                    var emp = employee.Trim();
                    filters.Add(builder.Or(
                        builder.Eq(x => x.Username, emp),
                        builder.Eq(x => x.EmployeeId, emp),
                        builder.Regex(x => x.EmployeeName, new MongoDB.Bson.BsonRegularExpression(emp, "i")),
                        builder.Regex(x => x.Username, new MongoDB.Bson.BsonRegularExpression(emp, "i"))
                    ));
                }

                if (startDate.HasValue && startDate.Value.Year >= 2000)
                {
                    filters.Add(builder.Gte(x => x.Timestamp, startDate.Value.Date));
                }

                if (endDate.HasValue && endDate.Value.Year >= 2000)
                {
                    filters.Add(builder.Lte(x => x.Timestamp, endDate.Value.Date.AddDays(1).AddTicks(-1)));
                }

                if (!string.IsNullOrWhiteSpace(ipAddress))
                {
                    filters.Add(builder.Eq(x => x.IpAddress, ipAddress.Trim()));
                }

                var baseFilter = filters.Count switch
                {
                    0 => builder.Empty,
                    1 => filters[0],
                    _ => builder.And(filters)
                };

                var combinedFilter = builder.And(baseFilter, GetTenantFilter());

                var totalCount = await _collection.CountDocumentsAsync(combinedFilter);
                var items = await _collection.Find(combinedFilter)
                    .SortByDescending(x => x.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIT REPOSITORY ERROR] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                var fallbackFilter = GetTenantFilter();
                var fallbackItems = await _collection.Find(fallbackFilter)
                    .SortByDescending(x => x.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();
                var fallbackCount = await _collection.CountDocumentsAsync(fallbackFilter);
                return (fallbackItems, fallbackCount);
            }
        }

        public async Task<AuditLogStats> GetLogStatsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var builder = Builders<AuditLog>.Filter;
            var tenantFilter = GetTenantFilter();

            var totalLogs = await _collection.CountDocumentsAsync(tenantFilter);
            var todayLogs = await _collection.CountDocumentsAsync(builder.And(builder.Gte(x => x.Timestamp, today), tenantFilter));

            var successLogs = await _collection.CountDocumentsAsync(
                builder.And(
                    builder.Or(
                        builder.Eq(x => x.Status, "Success"),
                        builder.Eq(x => x.LogLevel, "Success"),
                        builder.Eq(x => x.Status, null),
                        builder.Exists(x => x.Status, false)
                    ),
                    tenantFilter
                )
            );
            var warningLogs = await _collection.CountDocumentsAsync(builder.And(builder.Or(builder.Eq(x => x.Status, "Warning"), builder.Eq(x => x.LogLevel, "Warning")), tenantFilter));
            var errorLogs = await _collection.CountDocumentsAsync(builder.And(builder.Or(builder.Eq(x => x.Status, "Error"), builder.Eq(x => x.LogLevel, "Error"), builder.Eq(x => x.Status, "Failed")), tenantFilter));
            var criticalLogs = await _collection.CountDocumentsAsync(builder.And(builder.Or(builder.Eq(x => x.Status, "Critical"), builder.Eq(x => x.LogLevel, "Critical")), tenantFilter));

            var todayLoginsFilter = builder.And(builder.Gte(x => x.Timestamp, today), builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression("login", "i")), tenantFilter);
            var todayLogins = await _collection.CountDocumentsAsync(todayLoginsFilter);

            var todayStockFilter = builder.And(builder.Gte(x => x.Timestamp, today), (builder.Regex(x => x.Module, new MongoDB.Bson.BsonRegularExpression("stock", "i")) | builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression("stock", "i"))), tenantFilter);
            var todayStockChanges = await _collection.CountDocumentsAsync(todayStockFilter);

            var todaySalesFilter = builder.And(builder.Gte(x => x.Timestamp, today), (builder.Regex(x => x.Module, new MongoDB.Bson.BsonRegularExpression("sale|pos|invoice", "i")) | builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression("sale|invoice", "i"))), tenantFilter);
            var todaySales = await _collection.CountDocumentsAsync(todaySalesFilter);

            return new AuditLogStats
            {
                TotalLogs = totalLogs,
                TodayLogs = todayLogs,
                SuccessLogs = successLogs,
                WarningLogs = warningLogs,
                ErrorLogs = errorLogs,
                CriticalLogs = criticalLogs,
                TodayLogins = todayLogins,
                TodayStockChanges = todayStockChanges,
                TodaySales = todaySales
            };
        }

        public async Task<long> DeleteLogsOlderThanAsync(int days)
        {
            if (days <= 0)
            {
                return await ClearAllLogsAsync();
            }

            var cutoff = DateTime.UtcNow.AddDays(-days);
            var filter = Builders<AuditLog>.Filter.And(
                Builders<AuditLog>.Filter.Lt(x => x.Timestamp, cutoff),
                GetTenantFilter()
            );
            var result = await _collection.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        public async Task<long> ClearAllLogsAsync()
        {
            var result = await _collection.DeleteManyAsync(GetTenantFilter());
            return result.DeletedCount;
        }

        public async Task<long> DeleteLogsByIdsAsync(IEnumerable<string> ids)
        {
            if (ids == null || !ids.Any()) return 0;
            var filter = Builders<AuditLog>.Filter.And(
                Builders<AuditLog>.Filter.In(x => x.Id, ids),
                GetTenantFilter()
            );
            var result = await _collection.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        private static string GetModuleSearchPattern(string module) => module.ToLower() switch
        {
            "authentication" => "auth|login|logout",
            "employee management" => "employee|user|employees",
            "categories" => "category|categories",
            "products" => "product|products",
            "stock management" => "stock",
            "pos billing & invoices" => "pos|sale|invoice|billing",
            "global system settings" => "setting|config",
            "permissions" => "permission",
            "system" => "system",
            "notifications" => "notification",
            "security" => "security|firewall",
            _ => module
        };
    }
}
