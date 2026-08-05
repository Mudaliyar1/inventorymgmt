using System.Collections.Generic;
using InventoryManagementSystem.ViewModels;

namespace InventoryManagementSystem.Interfaces
{
    public interface IPermissionDiscoveryService
    {
        IEnumerable<PermissionDescriptor> DiscoverAllPermissions();
        IEnumerable<ModulePermissionsGroup> GetGroupedPermissions();
        string GetPermissionKey(string controllerName, string actionName);
    }
}
