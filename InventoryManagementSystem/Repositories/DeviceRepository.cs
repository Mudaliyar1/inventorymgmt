using MongoDB.Bson;
using MongoDB.Driver;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Repositories
{
    public class DeviceRepository : BaseRepository<Device>, IDeviceRepository
    {
        public DeviceRepository(MongoDbContext context) : base(context, "Devices")
        {
        }

        public async Task<Device?> GetByImeiAsync(string imei)
        {
            if (string.IsNullOrWhiteSpace(imei)) return null;
            var filter = Builders<Device>.Filter.Or(
                Builders<Device>.Filter.Eq(d => d.IMEI1, imei.Trim()),
                Builders<Device>.Filter.Eq(d => d.IMEI2, imei.Trim())
            );
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<Device?> GetBySerialAsync(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber)) return null;
            var filter = Builders<Device>.Filter.Eq(d => d.SerialNumber, serialNumber.Trim());
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Device>> GetAvailableDevicesForProductAsync(string productId)
        {
            var filter = Builders<Device>.Filter.And(
                Builders<Device>.Filter.Eq(d => d.ProductId, productId),
                Builders<Device>.Filter.Eq(d => d.Status, "InStock")
            );
            return await _collection.Find(filter).SortBy(d => d.CreatedDate).ToListAsync();
        }

        public async Task<IEnumerable<Device>> GetDevicesByStatusAsync(string status)
        {
            var filter = Builders<Device>.Filter.Eq(d => d.Status, status);
            return await _collection.Find(filter).SortByDescending(d => d.UpdatedDate).ToListAsync();
        }

        public async Task<IEnumerable<Device>> GetPagedDevicesAsync(string? search, string? productId, string? status, string? brand, int page, int pageSize)
        {
            var filter = BuildFilter(search, productId, status, brand);
            return await _collection.Find(filter)
                .SortByDescending(d => d.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<long> GetFilteredCountAsync(string? search, string? productId, string? status, string? brand)
        {
            var filter = BuildFilter(search, productId, status, brand);
            return await _collection.CountDocumentsAsync(filter);
        }

        public async Task<bool> IsImeiExistsAsync(string imei, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(imei)) return false;
            var cleanImei = imei.Trim();
            var filter = Builders<Device>.Filter.Or(
                Builders<Device>.Filter.Eq(d => d.IMEI1, cleanImei),
                Builders<Device>.Filter.Eq(d => d.IMEI2, cleanImei)
            );

            if (!string.IsNullOrEmpty(excludeId))
            {
                filter = Builders<Device>.Filter.And(
                    filter,
                    Builders<Device>.Filter.Ne(d => d.Id, excludeId)
                );
            }

            return await _collection.CountDocumentsAsync(filter) > 0;
        }

        public async Task<bool> UpdateStatusAsync(string deviceId, string status, string? invoiceNumber = null, string? customerId = null, string? customerName = null, string? customerPhone = null)
        {
            var update = Builders<Device>.Update
                .Set(d => d.Status, status)
                .Set(d => d.UpdatedDate, DateTime.UtcNow);

            if (!string.IsNullOrEmpty(invoiceNumber)) update = update.Set(d => d.InvoiceNumber, invoiceNumber);
            if (!string.IsNullOrEmpty(customerId)) update = update.Set(d => d.CustomerId, customerId);
            if (!string.IsNullOrEmpty(customerName)) update = update.Set(d => d.CustomerName, customerName);
            if (!string.IsNullOrEmpty(customerPhone)) update = update.Set(d => d.CustomerPhone, customerPhone);
            if (status == "Sold") update = update.Set(d => d.SoldDate, DateTime.UtcNow);

            var res = await _collection.UpdateOneAsync(Builders<Device>.Filter.Eq(d => d.Id, deviceId), update);
            return res.ModifiedCount > 0;
        }

        private FilterDefinition<Device> BuildFilter(string? search, string? productId, string? status, string? brand)
        {
            var filter = Builders<Device>.Filter.Empty;

            if (!string.IsNullOrEmpty(productId))
            {
                filter = Builders<Device>.Filter.And(filter, Builders<Device>.Filter.Eq(d => d.ProductId, productId));
            }
            if (!string.IsNullOrEmpty(status))
            {
                filter = Builders<Device>.Filter.And(filter, Builders<Device>.Filter.Eq(d => d.Status, status));
            }
            if (!string.IsNullOrEmpty(brand))
            {
                filter = Builders<Device>.Filter.And(filter, Builders<Device>.Filter.Eq(d => d.Brand, brand));
            }

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.Trim();
                var searchFilter = Builders<Device>.Filter.Or(
                    Builders<Device>.Filter.Regex(d => d.IMEI1, new BsonRegularExpression(s, "i")),
                    Builders<Device>.Filter.Regex(d => d.IMEI2, new BsonRegularExpression(s, "i")),
                    Builders<Device>.Filter.Regex(d => d.SerialNumber, new BsonRegularExpression(s, "i")),
                    Builders<Device>.Filter.Regex(d => d.ProductName, new BsonRegularExpression(s, "i")),
                    Builders<Device>.Filter.Regex(d => d.ModelName, new BsonRegularExpression(s, "i")),
                    Builders<Device>.Filter.Regex(d => d.CustomerName, new BsonRegularExpression(s, "i")),
                    Builders<Device>.Filter.Regex(d => d.CustomerPhone, new BsonRegularExpression(s, "i"))
                );
                filter = Builders<Device>.Filter.And(filter, searchFilter);
            }

            return filter;
        }
    }
}
