using InventoryManagementSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IFeatureCatalogService
    {
        Task<List<FeatureDefinition>> GetAllFeaturesAsync();
        Task<List<FeatureDefinition>> GetActiveFeaturesAsync();
        Task<Dictionary<string, List<FeatureDefinition>>> GetGroupedFeaturesAsync();
        Task SeedDefaultFeaturesAsync();
        Task<bool> IsFeatureEnabledForTenantAsync(string tenantId, string controllerName, string actionName = "Index");
        Task<List<string>> GetEnabledFeatureKeysForTenantAsync(string tenantId);
        FeatureDefinition? GetFeatureByControllerAndAction(string controllerName, string actionName = "Index");
    }
}
