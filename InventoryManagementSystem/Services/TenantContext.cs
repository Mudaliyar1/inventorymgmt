using Microsoft.AspNetCore.Http;
using InventoryManagementSystem.Interfaces;
using System.Security.Claims;

namespace InventoryManagementSystem.Services
{
    public class TenantContext : ITenantContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private string? _overrideTenantId;

        public TenantContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? TenantId
        {
            get
            {
                if (!string.IsNullOrEmpty(_overrideTenantId))
                {
                    return _overrideTenantId;
                }

                var user = _httpContextAccessor.HttpContext?.User;
                if (user != null && user.Identity != null && user.Identity.IsAuthenticated)
                {
                    // If SuperAdmin, TenantId can be null or overridden for impersonation
                    var role = user.FindFirst(ClaimTypes.Role)?.Value;
                    if (role == "SuperAdmin")
                    {
                        var impersonatedTenant = user.FindFirst("ImpersonatedTenantId")?.Value;
                        return !string.IsNullOrEmpty(impersonatedTenant) ? impersonatedTenant : null;
                    }

                    var tenantClaim = user.FindFirst("TenantId")?.Value 
                                   ?? user.FindFirst(ClaimTypes.GroupSid)?.Value;
                    return tenantClaim;
                }

                return null;
            }
        }

        public bool IsSuperAdmin
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user != null && user.Identity != null && user.Identity.IsAuthenticated)
                {
                    var role = user.FindFirst(ClaimTypes.Role)?.Value;
                    return role == "SuperAdmin";
                }
                return false;
            }
        }

        public void SetTenantId(string? tenantId)
        {
            _overrideTenantId = tenantId;
        }
    }
}
