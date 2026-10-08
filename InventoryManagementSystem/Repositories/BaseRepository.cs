using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class BaseRepository<T> : IBaseRepository<T> where T : class
    {
        protected readonly MongoDbContext _context;
        protected readonly IMongoCollection<T> _collection;
        protected readonly ITenantContext? _tenantContext;

        public BaseRepository(MongoDbContext context, string collectionName, ITenantContext? tenantContext = null)
        {
            _context = context;
            _collection = _context.GetCollection<T>(collectionName);
            _tenantContext = tenantContext;
        }

        protected FilterDefinition<T> GetTenantFilter()
        {
            if (_tenantContext != null && !_tenantContext.IsSuperAdmin && typeof(ITenantEntity).IsAssignableFrom(typeof(T)))
            {
                var tenantId = _tenantContext.TenantId;
                if (!string.IsNullOrEmpty(tenantId))
                {
                    return Builders<T>.Filter.Eq("TenantId", tenantId);
                }
            }
            return Builders<T>.Filter.Empty;
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            var filter = GetTenantFilter();
            return await _collection.Find(filter).ToListAsync();
        }

        public virtual async Task<T?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            FilterDefinition<T> idFilter;
            if (MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            {
                idFilter = Builders<T>.Filter.Eq("_id", objectId);
            }
            else
            {
                idFilter = Builders<T>.Filter.Eq("_id", id);
            }

            var combinedFilter = Builders<T>.Filter.And(idFilter, GetTenantFilter());
            return await _collection.Find(combinedFilter).FirstOrDefaultAsync();
        }

        public virtual async Task CreateAsync(T entity)
        {
            if (entity is ITenantEntity tenantEntity && string.IsNullOrEmpty(tenantEntity.TenantId) && _tenantContext != null && !string.IsNullOrEmpty(_tenantContext.TenantId))
            {
                tenantEntity.TenantId = _tenantContext.TenantId;
            }
            await _collection.InsertOneAsync(entity);
        }

        public virtual async Task UpdateAsync(string id, T entity)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            FilterDefinition<T> idFilter;
            if (MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            {
                idFilter = Builders<T>.Filter.Eq("_id", objectId);
            }
            else
            {
                idFilter = Builders<T>.Filter.Eq("_id", id);
            }

            if (entity is ITenantEntity tenantEntity && string.IsNullOrEmpty(tenantEntity.TenantId) && _tenantContext != null && !string.IsNullOrEmpty(_tenantContext.TenantId))
            {
                tenantEntity.TenantId = _tenantContext.TenantId;
            }

            var combinedFilter = Builders<T>.Filter.And(idFilter, GetTenantFilter());
            var result = await _collection.ReplaceOneAsync(combinedFilter, entity);
            Console.WriteLine($"[REPOSITORY DIAGNOSTICS] UpdateAsync matched: {result.MatchedCount}, modified: {result.ModifiedCount}");
        }

        public virtual async Task DeleteAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            FilterDefinition<T> idFilter;
            if (MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            {
                idFilter = Builders<T>.Filter.Eq("_id", objectId);
            }
            else
            {
                idFilter = Builders<T>.Filter.Eq("_id", id);
            }

            var combinedFilter = Builders<T>.Filter.And(idFilter, GetTenantFilter());
            await _collection.DeleteOneAsync(combinedFilter);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            var expressionFilter = Builders<T>.Filter.Where(predicate);
            var combinedFilter = Builders<T>.Filter.And(expressionFilter, GetTenantFilter());
            return await _collection.Find(combinedFilter).ToListAsync();
        }

        public virtual async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            var baseFilter = predicate != null ? Builders<T>.Filter.Where(predicate) : Builders<T>.Filter.Empty;
            var combinedFilter = Builders<T>.Filter.And(baseFilter, GetTenantFilter());
            return await _collection.CountDocumentsAsync(combinedFilter);
        }
    }
}
