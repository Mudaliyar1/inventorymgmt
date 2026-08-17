using MongoDB.Bson;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class RepairRepository : BaseRepository<RepairTicket>, IRepairRepository
    {
        public RepairRepository(MongoDbContext context) : base(context, "RepairTickets")
        {
        }

        public async Task<IEnumerable<RepairTicket>> GetPagedRepairsAsync(string? search, string? status, int page, int pageSize)
        {
            var filter = BuildFilter(search, status);
            return await _collection.Find(filter)
                .SortByDescending(r => r.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? status)
        {
            var filter = BuildFilter(search, status);
            return await _collection.CountDocumentsAsync(filter);
        }

        private FilterDefinition<RepairTicket> BuildFilter(string? search, string? status)
        {
            var filter = Builders<RepairTicket>.Filter.Empty;

            if (!string.IsNullOrEmpty(status))
            {
                filter = Builders<RepairTicket>.Filter.And(filter, Builders<RepairTicket>.Filter.Eq(r => r.Status, status));
            }

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.Trim();
                var searchFilter = Builders<RepairTicket>.Filter.Or(
                    Builders<RepairTicket>.Filter.Regex(r => r.TicketNumber, new BsonRegularExpression(s, "i")),
                    Builders<RepairTicket>.Filter.Regex(r => r.IMEI, new BsonRegularExpression(s, "i")),
                    Builders<RepairTicket>.Filter.Regex(r => r.CustomerName, new BsonRegularExpression(s, "i")),
                    Builders<RepairTicket>.Filter.Regex(r => r.CustomerPhone, new BsonRegularExpression(s, "i")),
                    Builders<RepairTicket>.Filter.Regex(r => r.DeviceModel, new BsonRegularExpression(s, "i")),
                    Builders<RepairTicket>.Filter.Regex(r => r.TechnicianName, new BsonRegularExpression(s, "i"))
                );
                filter = Builders<RepairTicket>.Filter.And(filter, searchFilter);
            }

            return filter;
        }
    }
}
