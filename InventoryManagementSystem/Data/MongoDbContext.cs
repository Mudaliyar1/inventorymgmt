using Microsoft.Extensions.Options;
using MongoDB.Driver;
using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IMongoClient mongoClient, IOptions<MongoDbSettings> settings)
        {
            _database = mongoClient.GetDatabase(settings.Value.DatabaseName);
        }

        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            _database = client.GetDatabase(settings.Value.DatabaseName);
        }

        public IMongoCollection<T> GetCollection<T>(string name)
        {
            return _database.GetCollection<T>(name);
        }

        // Strongly-typed collections
        public IMongoCollection<User> Users => GetCollection<User>("Users");
        public IMongoCollection<Role> Roles => GetCollection<Role>("Roles");
        public IMongoCollection<AuditLog> AuditLogs => GetCollection<AuditLog>("AuditLogs");
        public IMongoCollection<Notification> Notifications => GetCollection<Notification>("Notifications");
        public IMongoCollection<Settings> Settings => GetCollection<Settings>("Settings");
        public IMongoCollection<Category> Categories => GetCollection<Category>("Categories");
        public IMongoCollection<Product> Products => GetCollection<Product>("Products");
        public IMongoCollection<StockTransaction> StockTransactions => GetCollection<StockTransaction>("StockTransactions");
        public IMongoCollection<Sale> Sales => GetCollection<Sale>("Sales");

        /// <summary>
        /// Automatically verifies and creates database indexes on startup for fast queries.
        /// </summary>
        public async Task InitializeIndexesAsync()
        {
            try
            {
                // Products Indexes
                var productCodeIndex = new CreateIndexModel<Product>(
                    Builders<Product>.IndexKeys.Ascending(p => p.Code),
                    new CreateIndexOptions { Unique = true, Sparse = true });
                var productCatIndex = new CreateIndexModel<Product>(
                    Builders<Product>.IndexKeys.Ascending(p => p.CategoryId));
                var productStatusIndex = new CreateIndexModel<Product>(
                    Builders<Product>.IndexKeys.Ascending(p => p.Status));
                await Products.Indexes.CreateManyAsync(new[] { productCodeIndex, productCatIndex, productStatusIndex });

                // Categories Indexes
                var categoryNameIndex = new CreateIndexModel<Category>(
                    Builders<Category>.IndexKeys.Ascending(c => c.Name),
                    new CreateIndexOptions { Unique = true, Sparse = true });
                await Categories.Indexes.CreateOneAsync(categoryNameIndex);

                // StockTransactions Indexes
                var txTimestampIndex = new CreateIndexModel<StockTransaction>(
                    Builders<StockTransaction>.IndexKeys.Descending(t => t.Timestamp));
                var txProductIndex = new CreateIndexModel<StockTransaction>(
                    Builders<StockTransaction>.IndexKeys.Ascending(t => t.ProductId).Descending(t => t.Timestamp));
                await StockTransactions.Indexes.CreateManyAsync(new[] { txTimestampIndex, txProductIndex });

                // Sales Indexes
                var saleInvoiceIndex = new CreateIndexModel<Sale>(
                    Builders<Sale>.IndexKeys.Ascending(s => s.InvoiceNumber),
                    new CreateIndexOptions { Unique = true, Sparse = true });
                var saleDateIndex = new CreateIndexModel<Sale>(
                    Builders<Sale>.IndexKeys.Descending(s => s.Date));
                await Sales.Indexes.CreateManyAsync(new[] { saleInvoiceIndex, saleDateIndex });

                // Notifications Indexes
                var notifReadIndex = new CreateIndexModel<Notification>(
                    Builders<Notification>.IndexKeys.Ascending(n => n.IsRead).Descending(n => n.Timestamp));
                await Notifications.Indexes.CreateOneAsync(notifReadIndex);

                // Users Indexes
                var userUsernameIndex = new CreateIndexModel<User>(
                    Builders<User>.IndexKeys.Ascending(u => u.Username),
                    new CreateIndexOptions { Unique = true, Sparse = true });
                await Users.Indexes.CreateOneAsync(userUsernameIndex);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[MongoDB Indexing Notice] Index creation skipped or partially applied: {ex.Message}");
            }
        }
    }
}
