using Microsoft.Extensions.Options;
using MongoDB.Driver;
using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            _database = client.GetDatabase(settings.Value.DatabaseName);
        }

        public IMongoCollection<T> GetCollection<T>(string name)
        {
            return _database.GetCollection<T>(name);
        }

        // Strongly-typed collections for Phase 1
        public IMongoCollection<User> Users => GetCollection<User>("Users");
        public IMongoCollection<Role> Roles => GetCollection<Role>("Roles");
        public IMongoCollection<AuditLog> AuditLogs => GetCollection<AuditLog>("AuditLogs");
        public IMongoCollection<Notification> Notifications => GetCollection<Notification>("Notifications");
        public IMongoCollection<Settings> Settings => GetCollection<Settings>("Settings");
    }
}
