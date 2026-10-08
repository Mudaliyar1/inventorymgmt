using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Repositories
{
    public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
    {
        public CategoryRepository(MongoDbContext context, ITenantContext tenantContext) : base(context, "Categories", tenantContext)
        {
        }
    }
}
