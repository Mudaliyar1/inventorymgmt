using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(MongoDbContext context, ITenantContext tenantContext) : base(context, "Users", tenantContext)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var combinedFilter = Builders<User>.Filter.And(filter, GetTenantFilter());
            return await _collection.Find(combinedFilter).FirstOrDefaultAsync();
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            var combinedFilter = Builders<User>.Filter.And(filter, GetTenantFilter());
            return await _collection.Find(combinedFilter).FirstOrDefaultAsync();
        }
    }
}
