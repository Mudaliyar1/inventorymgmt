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
        public NotificationRepository(MongoDbContext context) : base(context, "Notifications")
        {
        }

        public async Task<IEnumerable<Notification>> GetUnreadNotificationsAsync()
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.IsRead, false);
            return await _collection.Find(filter)
                .SortByDescending(n => n.Timestamp)
                .ToListAsync();
        }

        public async Task MarkAllAsReadAsync()
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.IsRead, false);
            var update = Builders<Notification>.Update.Set(n => n.IsRead, true);
            await _collection.UpdateManyAsync(filter, update);
        }
    }
}
