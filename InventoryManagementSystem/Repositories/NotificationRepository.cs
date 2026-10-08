using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class NotificationRepository : BaseRepository<Notification>, INotificationRepository
    {
        public NotificationRepository(MongoDbContext context, ITenantContext tenantContext) : base(context, "Notifications", tenantContext)
        {
        }

        public async Task<IEnumerable<Notification>> GetUnreadNotificationsAsync()
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.IsRead, false),
                GetTenantFilter()
            );
            return await _collection.Find(filter)
                .SortByDescending(n => n.Timestamp)
                .Limit(10)
                .ToListAsync();
        }

        public async Task MarkAllAsReadAsync()
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.IsRead, false),
                GetTenantFilter()
            );
            var update = Builders<Notification>.Update.Set(n => n.IsRead, true);
            await _collection.UpdateManyAsync(filter, update);
        }
    }
}
