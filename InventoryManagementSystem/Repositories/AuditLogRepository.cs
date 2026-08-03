using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class AuditLogRepository : BaseRepository<AuditLog>, IAuditLogRepository
    {
        public AuditLogRepository(MongoDbContext context) : base(context, "AuditLogs")
        {
        }

        public async Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count)
        {
            return await _collection.Find(_ => true)
                .SortByDescending(l => l.Timestamp)
                .Limit(count)
                .ToListAsync();
        }
    }
}
