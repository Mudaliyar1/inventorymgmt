using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Filters
{
    public class PermissionAuthorizeFilter : IAsyncActionFilter
    {
        private readonly IPermissionService _permissionService;

        public PermissionAuthorizeFilter(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Check if AllowAnonymous is present on action or controller
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await next();
                return;
            }

            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
            var actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;

            // Allow AccountController (Login, Logout, AccessDenied, etc.) for all users
            if (controllerName.Equals("Account", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var user = context.HttpContext.User;

            // 1. Unauthenticated -> Redirect to Login
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                // Retrieve live user state (using fast memory cache with instant admin invalidation)
                var liveState = await _permissionService.GetLiveUserStateAsync(userId);

                // Forced Logout & Session Invalidation if locked or deleted
                if (!liveState.IsValid || liveState.IsLocked)
                {
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                    var factory = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>();
                    var tempData = factory.GetTempData(context.HttpContext);
                    tempData["ToastMessage"] = "Your account permissions or status have changed. Please contact administrator if you believe this is an error.";
                    tempData["ToastType"] = "warning";

                    context.Result = new RedirectToActionResult("Login", "Account", null);
                    return;
                }
            }

            // 2. Super Admin (Admin role) -> Full access everywhere
            if (user.IsInRole(Role.Admin))
            {
                await next();
                return;
            }

            // Allow NotificationsController for all active authenticated users
            if (controllerName.Equals("Notifications", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // 3. Verify Live Employee Permission
            var hasAccess = await _permissionService.HasLivePermissionAsync(user, controllerName, actionName);

            if (!hasAccess)
            {
                var auditLogService = context.HttpContext.RequestServices.GetService<IAuditLogService>();
                if (auditLogService != null)
                {
                    var username = user.Identity?.Name ?? "Unknown";
                    await auditLogService.LogSecurityEventAsync(
                        "Unauthorized Access Attempt",
                        $"Employee @{username} attempted unauthorized action '{actionName}' on module '{controllerName}'",
                        "Failed",
                        "Warning");
                }

                // Short-circuit with 403 Access Denied view
                context.HttpContext.Response.StatusCode = 403;
                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/AccessDenied.cshtml",
                    StatusCode = 403
                };
                return;
            }

            await next();
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class PermissionAuthorizeAttribute : TypeFilterAttribute
    {
        public PermissionAuthorizeAttribute() : base(typeof(PermissionAuthorizeFilter))
        {
        }
    }
}
