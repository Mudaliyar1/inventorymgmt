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
        public AuditLogRepository(MongoDbContext context) : base(context, "AuditLogs")
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
            return await _collection.Find(_ => true)
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
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added Keyword Filter: '{k}'");
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
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added Module Filter: '{m}' -> Pattern '{modSearch}'");
                    filters.Add(builder.Regex(x => x.Module, new MongoDB.Bson.BsonRegularExpression(modSearch, "i")));
                }

                if (!string.IsNullOrWhiteSpace(action))
                {
                    var a = action.Trim();
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added Action Filter: '{a}'");
                    filters.Add(builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression(a, "i")));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    var st = status.Trim();
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added Status Filter: '{st}'");
                    filters.Add(builder.Or(
                        builder.Eq(x => x.Status, st),
                        builder.Eq(x => x.LogLevel, st),
                        builder.Regex(x => x.Status, new MongoDB.Bson.BsonRegularExpression(st, "i"))
                    ));
                }

                if (!string.IsNullOrWhiteSpace(logLevel))
                {
                    var ll = logLevel.Trim();
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added LogLevel Filter: '{ll}'");
                    filters.Add(builder.Or(
                        builder.Eq(x => x.LogLevel, ll),
                        builder.Eq(x => x.Status, ll),
                        builder.Regex(x => x.LogLevel, new MongoDB.Bson.BsonRegularExpression(ll, "i"))
                    ));
                }

                if (!string.IsNullOrWhiteSpace(employee))
                {
                    var emp = employee.Trim();
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added Employee Filter: '{emp}'");
                    filters.Add(builder.Or(
                        builder.Eq(x => x.Username, emp),
                        builder.Eq(x => x.EmployeeId, emp),
                        builder.Regex(x => x.EmployeeName, new MongoDB.Bson.BsonRegularExpression(emp, "i")),
                        builder.Regex(x => x.Username, new MongoDB.Bson.BsonRegularExpression(emp, "i"))
                    ));
                }

                if (startDate.HasValue && startDate.Value.Year >= 2000)
                {
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added StartDate Filter: '{startDate.Value:yyyy-MM-dd}'");
                    filters.Add(builder.Gte(x => x.Timestamp, startDate.Value.Date));
                }

                if (endDate.HasValue && endDate.Value.Year >= 2000)
                {
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added EndDate Filter: '{endDate.Value:yyyy-MM-dd}'");
                    filters.Add(builder.Lte(x => x.Timestamp, endDate.Value.Date.AddDays(1).AddTicks(-1)));
                }

                if (!string.IsNullOrWhiteSpace(ipAddress))
                {
                    Console.WriteLine($"[FILTER DIAGNOSTIC] Added IpAddress Filter: '{ipAddress}'");
                    filters.Add(builder.Eq(x => x.IpAddress, ipAddress.Trim()));
                }

                FilterDefinition<AuditLog> filter = filters.Count switch
                {
                    0 => builder.Empty,
                    1 => filters[0],
                    _ => builder.And(filters)
                };

                var renderedFilter = filter.ToString();
                Console.WriteLine($"[AUDIT REPOSITORY DIAGNOSTIC] filters.Count={filters.Count}, RenderedFilter={renderedFilter}");

                var totalCount = await _collection.CountDocumentsAsync(filter);
                var items = await _collection.Find(filter)
                    .SortByDescending(x => x.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();

                Console.WriteLine($"[AUDIT REPOSITORY DIAGNOSTIC] Query executed successfully. totalCount={totalCount}, items.Count={items.Count}");

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIT REPOSITORY ERROR] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                var fallbackItems = await _collection.Find(FilterDefinition<AuditLog>.Empty)
                    .SortByDescending(x => x.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();
                var fallbackCount = await _collection.CountDocumentsAsync(FilterDefinition<AuditLog>.Empty);
                return (fallbackItems, fallbackCount);
            }
        }

        public async Task<AuditLogStats> GetLogStatsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var builder = Builders<AuditLog>.Filter;

            var totalLogs = await _collection.CountDocumentsAsync(builder.Empty);
            var todayLogs = await _collection.CountDocumentsAsync(builder.Gte(x => x.Timestamp, today));

            var successLogs = await _collection.CountDocumentsAsync(
                builder.Or(
                    builder.Eq(x => x.Status, "Success"),
                    builder.Eq(x => x.LogLevel, "Success"),
                    builder.Eq(x => x.Status, null),
                    builder.Exists(x => x.Status, false)
                )
            );
            var warningLogs = await _collection.CountDocumentsAsync(builder.Or(builder.Eq(x => x.Status, "Warning"), builder.Eq(x => x.LogLevel, "Warning")));
            var errorLogs = await _collection.CountDocumentsAsync(builder.Or(builder.Eq(x => x.Status, "Error"), builder.Eq(x => x.LogLevel, "Error"), builder.Eq(x => x.Status, "Failed")));
            var criticalLogs = await _collection.CountDocumentsAsync(builder.Or(builder.Eq(x => x.Status, "Critical"), builder.Eq(x => x.LogLevel, "Critical")));

            var todayLoginsFilter = builder.Gte(x => x.Timestamp, today) & builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression("login", "i"));
            var todayLogins = await _collection.CountDocumentsAsync(todayLoginsFilter);

            var todayStockFilter = builder.Gte(x => x.Timestamp, today) & (builder.Regex(x => x.Module, new MongoDB.Bson.BsonRegularExpression("stock", "i")) | builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression("stock", "i")));
            var todayStockChanges = await _collection.CountDocumentsAsync(todayStockFilter);

            var todaySalesFilter = builder.Gte(x => x.Timestamp, today) & (builder.Regex(x => x.Module, new MongoDB.Bson.BsonRegularExpression("sale|pos|invoice", "i")) | builder.Regex(x => x.Action, new MongoDB.Bson.BsonRegularExpression("sale|invoice", "i")));
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
            var filter = Builders<AuditLog>.Filter.Lt(x => x.Timestamp, cutoff);
            var result = await _collection.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        public async Task<long> ClearAllLogsAsync()
        {
            var result = await _collection.DeleteManyAsync(FilterDefinition<AuditLog>.Empty);
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
