using InventoryManagementSystem.DTOs;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public interface IMobileSpecSearchService
    {
        Task<MobileSpecSearchResultDto> SearchSpecificationsAsync(
            string brand,
            string modelName,
            string variant,
            bool allowThirdPartyFallback = false,
            string? customUrl = null,
            bool forceRefresh = false);
    }
}
