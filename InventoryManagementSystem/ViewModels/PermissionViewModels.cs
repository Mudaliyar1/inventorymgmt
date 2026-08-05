using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels
{
    public class PermissionDescriptor
    {
        public string PermissionKey { get; set; } = string.Empty; // e.g. "Product.View", "Product.Create"
        public string ModuleName { get; set; } = string.Empty;     // e.g. "Products"
        public string ControllerName { get; set; } = string.Empty; // e.g. "Product"
        public string ActionName { get; set; } = string.Empty;     // e.g. "Index", "Create"
        public string ActionType { get; set; } = string.Empty;     // View, Create, Edit, Delete, Export, Print, Approve, Cancel
        public string DisplayName { get; set; } = string.Empty;    // e.g. "View Products List"
        public string Description { get; set; } = string.Empty;
    }

    public class ModulePermissionsGroup
    {
        public string ModuleName { get; set; } = string.Empty;
        public string ControllerName { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-app-indicator";
        public List<PermissionDescriptor> Permissions { get; set; } = new List<PermissionDescriptor>();
    }
}
