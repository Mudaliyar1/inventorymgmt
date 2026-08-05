using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Interfaces
{
    public class LiveUserState
    {
        public bool IsValid { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public int PermissionVersion { get; set; } = 1;
        public string Role { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new List<string>();
    }

    public interface IPermissionService
    {
        bool HasPermission(ClaimsPrincipal user, string permissionKey);
        bool HasPermission(ClaimsPrincipal user, string controllerName, string actionName);
        bool HasModuleAccess(ClaimsPrincipal user, string controllerName);

        Task<LiveUserState> GetLiveUserStateAsync(string userId);
        Task<bool> HasLivePermissionAsync(ClaimsPrincipal user, string controllerName, string actionName);
        void InvalidateUserCache(string userId);

        Task<List<string>> GetUserPermissionsAsync(string userId);
        Task<bool> UpdateUserPermissionsAsync(string userId, List<string> permissions);
    }
}
