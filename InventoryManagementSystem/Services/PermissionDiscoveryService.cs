using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class PermissionDiscoveryService : IPermissionDiscoveryService
    {
        public IEnumerable<PermissionDescriptor> DiscoverAllPermissions()
        {
            var permissions = new List<PermissionDescriptor>();
            var assembly = typeof(PermissionDiscoveryService).Assembly;

            // Discover all non-abstract Controller classes
            var controllerTypes = assembly.GetTypes()
                .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
                .OrderBy(t => GetModuleOrder(GetControllerName(t.Name)));

            foreach (var controllerType in controllerTypes)
            {
                var controllerName = GetControllerName(controllerType.Name);
                var moduleName = GetModuleName(controllerName);

                // Get all public action methods returning IActionResult / Task<IActionResult> or non-inherited methods
                var actionMethods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName &&
                                !m.GetCustomAttributes<NonActionAttribute>().Any() &&
                                (typeof(IActionResult).IsAssignableFrom(m.ReturnType) ||
                                 typeof(Task<IActionResult>).IsAssignableFrom(m.ReturnType) ||
                                 typeof(ActionResult).IsAssignableFrom(m.ReturnType) ||
                                 typeof(Task<ActionResult>).IsAssignableFrom(m.ReturnType)))
                    .ToList();

                // Group action methods by logical action name to avoid duplicate HTTP verbs (e.g. GET Edit & POST Edit)
                var distinctActionNames = actionMethods.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var actionName in distinctActionNames)
                {
                    var actionType = DetermineActionType(actionName);
                    var key = GetPermissionKey(controllerName, actionName);

                    permissions.Add(new PermissionDescriptor
                    {
                        PermissionKey = key,
                        ModuleName = moduleName,
                        ControllerName = controllerName,
                        ActionName = actionName,
                        ActionType = actionType,
                        DisplayName = FormatDisplayName(controllerName, actionName, actionType),
                        Description = $"Grants permission to perform '{actionName}' on {moduleName} module."
                    });
                }
            }

            return permissions;
        }

        public IEnumerable<ModulePermissionsGroup> GetGroupedPermissions()
        {
            var allPermissions = DiscoverAllPermissions();

            return allPermissions
                .GroupBy(p => p.ModuleName)
                .Select(g => new ModulePermissionsGroup
                {
                    ModuleName = g.Key,
                    ControllerName = g.First().ControllerName,
                    IconClass = GetModuleIcon(g.First().ControllerName),
                    Permissions = g.OrderBy(p => GetActionTypeOrder(p.ActionType)).ThenBy(p => p.DisplayName).ToList()
                })
                .OrderBy(g => GetModuleOrder(g.ControllerName));
        }

        public string GetPermissionKey(string controllerName, string actionName)
        {
            if (string.IsNullOrWhiteSpace(controllerName)) return string.Empty;
            if (string.IsNullOrWhiteSpace(actionName)) actionName = "Index";

            return $"{controllerName}.{actionName}";
        }

        private static string GetControllerName(string typeName)
        {
            return typeName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                ? typeName[..^10]
                : typeName;
        }

        private static string GetModuleName(string controllerName) => controllerName switch
        {
            "Home" => "Dashboard & Overview",
            "Category" => "Categories",
            "Product" => "Products & Mobile Specs",
            "Imei" => "IMEI & Device Management",
            "Stock" => "Stock Management & Devices",
            "Sales" => "POS Billing & Invoices",
            "Customer" => "Customer Management",
            "Supplier" => "Supplier & Procurement",
            "Return" => "Device & Product Returns",
            "Exchange" => "Mobile Trade-In Exchange",
            "Repair" => "Service & Repair Tickets",
            "Report" => "Reports & Analytics",
            "Admin" => "Administrator Management",
            "User" => "Employee Management",
            "Settings" => "Global System Settings",
            "Notifications" => "System Notifications",
            "SystemLog" => "System Activity Logs",
            _ => SplitCamelCase(controllerName)
        };

        private static string GetModuleIcon(string controllerName) => controllerName switch
        {
            "Home" => "bi-grid-1x2",
            "Category" => "bi-tag",
            "Product" => "bi-phone",
            "Imei" => "bi-upc-scan",
            "Stock" => "bi-boxes",
            "Sales" => "bi-receipt-cutoff",
            "Customer" => "bi-person-vcard",
            "Supplier" => "bi-truck",
            "Return" => "bi-arrow-return-left",
            "Exchange" => "bi-arrow-repeat",
            "Repair" => "bi-wrench-adjustable",
            "Report" => "bi-bar-chart",
            "Admin" => "bi-shield-lock",
            "User" => "bi-people",
            "Settings" => "bi-sliders",
            "Notifications" => "bi-bell",
            "SystemLog" => "bi-journal-text",
            _ => "bi-app-indicator"
        };

        private static int GetModuleOrder(string controllerName) => controllerName switch
        {
            "Home" => 1,
            "Product" => 2,
            "Imei" => 3,
            "Category" => 4,
            "Stock" => 5,
            "Sales" => 6,
            "Exchange" => 7,
            "Return" => 8,
            "Repair" => 9,
            "Customer" => 10,
            "Supplier" => 11,
            "Report" => 12,
            "Admin" => 13,
            "User" => 14,
            "Settings" => 15,
            "SystemLog" => 16,
            _ => 99
        };

        private static string DetermineActionType(string actionName)
        {
            var name = actionName.ToLower();

            if (name.Contains("create") || name.Contains("add") || name.Contains("stockin") || name.Contains("stockout") || name.Contains("process")) return "Create";
            if (name.Contains("edit") || name.Contains("update") || name.Contains("adjust") || name.Contains("resetpassword") || name.Contains("toggle") || name.Contains("save")) return "Edit";
            if (name.Contains("delete") || name.Contains("remove") || name.Contains("bulkdelete")) return "Delete";
            if (name.Contains("download") || name.Contains("export") || name.Contains("pdf")) return "Export";
            if (name.Contains("print")) return "Print";
            if (name.Contains("approve")) return "Approve";
            if (name.Contains("cancel")) return "Cancel";

            return "View";
        }

        private static int GetActionTypeOrder(string actionType) => actionType switch
        {
            "View" => 1,
            "Create" => 2,
            "Edit" => 3,
            "Delete" => 4,
            "Export" => 5,
            "Print" => 6,
            "Approve" => 7,
            "Cancel" => 8,
            _ => 9
        };

        private static string FormatDisplayName(string controllerName, string actionName, string actionType)
        {
            if (actionName.Equals("Index", StringComparison.OrdinalIgnoreCase))
                return $"View {GetModuleName(controllerName)}";

            if (actionName.Equals("StockIn", StringComparison.OrdinalIgnoreCase))
                return "Perform Stock In";

            if (actionName.Equals("StockOut", StringComparison.OrdinalIgnoreCase))
                return "Perform Stock Out";

            if (actionName.Equals("Search", StringComparison.OrdinalIgnoreCase))
                return "Search & Track IMEI";

            if (actionName.Equals("Adjust", StringComparison.OrdinalIgnoreCase))
                return "Adjust Stock Quantities";

            if (actionName.Equals("InventorySummary", StringComparison.OrdinalIgnoreCase))
                return "View Inventory Summary & Valuation";

            if (actionName.Equals("History", StringComparison.OrdinalIgnoreCase))
                return "View Stock Audit History";

            if (actionName.Equals("DownloadInvoice", StringComparison.OrdinalIgnoreCase))
                return "Download PDF Invoice";

            return $"{actionType} - {SplitCamelCase(actionName)}";
        }

        private static string SplitCamelCase(string str)
        {
            return System.Text.RegularExpressions.Regex.Replace(str, "([a-z])([A-Z])", "$1 $2");
        }
    }
}
