using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Services
{
    public interface IPolicyService
    {
        Task<List<PolicyDocument>> GetAllPoliciesAsync(bool includeInactive = true);
        Task<List<PolicyDocument>> GetPublicPoliciesAsync();
        Task<PolicyDocument?> GetBySlugAsync(string slug, bool mustBePublishedAndActive = true);
        Task<PolicyDocument?> GetByIdAsync(string id);
        Task<PolicyDocument> CreatePolicyAsync(PolicyDocument policy, string adminUser);
        Task<PolicyDocument?> UpdatePolicyAsync(PolicyDocument policy, string adminUser, string changeNotes = "");
        Task<bool> TogglePublishAsync(string id, bool publish, string adminUser);
        Task<bool> ToggleActiveAsync(string id, bool active, string adminUser);
        Task<bool> DeletePolicyAsync(string id, string adminUser);
        Task<List<PolicyRevision>> GetRevisionsAsync(string policyId);
        Task<PolicyRevision?> GetRevisionByIdAsync(string revisionId);
        Task SeedDefaultPoliciesAsync();
        string ConvertMarkdownToSafeHtml(string markdown);
    }
}
