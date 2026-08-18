using System.Collections.Generic;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Interfaces
{
    public interface IManufacturerRegistryService
    {
        IEnumerable<ManufacturerSource> GetAllSources();
        ManufacturerSource? GetSourceForBrand(string brand);
        void RegisterSource(ManufacturerSource source);
    }
}
