namespace InventoryManagementSystem.Interfaces
{
    public interface ITenantContext
    {
        string? TenantId { get; }
        bool IsSuperAdmin { get; }
        void SetTenantId(string? tenantId);
    }
}
