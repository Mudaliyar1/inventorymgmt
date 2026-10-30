using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Services
{
    public class PolicyService : IPolicyService
    {
        private readonly MongoDbContext _context;
        private readonly IMongoCollection<PolicyDocument> _policiesCollection;
        private readonly IMongoCollection<PolicyRevision> _revisionsCollection;

        public PolicyService(MongoDbContext context)
        {
            _context = context;
            _policiesCollection = context.Policies;
            _revisionsCollection = context.PolicyRevisions;
        }

        public async Task<List<PolicyDocument>> GetAllPoliciesAsync(bool includeInactive = true)
        {
            var filter = includeInactive
                ? Builders<PolicyDocument>.Filter.Eq(p => p.IsDeleted, false)
                : Builders<PolicyDocument>.Filter.And(
                    Builders<PolicyDocument>.Filter.Eq(p => p.IsDeleted, false),
                    Builders<PolicyDocument>.Filter.Eq(p => p.IsActive, true));

            return await _policiesCollection
                .Find(filter)
                .SortBy(p => p.DisplayOrder)
                .ThenBy(p => p.Title)
                .ToListAsync();
        }

        public async Task<List<PolicyDocument>> GetPublicPoliciesAsync()
        {
            var filter = Builders<PolicyDocument>.Filter.And(
                Builders<PolicyDocument>.Filter.Eq(p => p.IsDeleted, false),
                Builders<PolicyDocument>.Filter.Eq(p => p.IsActive, true),
                Builders<PolicyDocument>.Filter.Eq(p => p.IsPublished, true));

            return await _policiesCollection
                .Find(filter)
                .SortBy(p => p.DisplayOrder)
                .ThenBy(p => p.Title)
                .ToListAsync();
        }

        public async Task<PolicyDocument?> GetBySlugAsync(string slug, bool mustBePublishedAndActive = true)
        {
            if (string.IsNullOrWhiteSpace(slug)) return null;

            var normalizedSlug = slug.Trim().ToLowerInvariant();
            var filterBuilder = Builders<PolicyDocument>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq(p => p.Slug, normalizedSlug),
                filterBuilder.Eq(p => p.IsDeleted, false));

            if (mustBePublishedAndActive)
            {
                filter = filterBuilder.And(
                    filter,
                    filterBuilder.Eq(p => p.IsActive, true),
                    filterBuilder.Eq(p => p.IsPublished, true));
            }

            return await _policiesCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<PolicyDocument?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return await _policiesCollection.Find(p => p.Id == id && !p.IsDeleted).FirstOrDefaultAsync();
        }

        public async Task<PolicyDocument> CreatePolicyAsync(PolicyDocument policy, string adminUser)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            if (string.IsNullOrWhiteSpace(policy.Id))
            {
                policy.Id = null;
            }

            policy.Slug = NormalizeSlug(string.IsNullOrWhiteSpace(policy.Slug) ? policy.Title : policy.Slug);
            
            // Ensure unique slug
            policy.Slug = await GenerateUniqueSlugAsync(policy.Slug, policy.Id);

            var now = DateTime.UtcNow;
            policy.CreatedAtUtc = now;
            policy.UpdatedAtUtc = now;
            policy.CreatedBy = string.IsNullOrWhiteSpace(adminUser) ? "SuperAdmin" : adminUser;
            policy.LastUpdatedBy = policy.CreatedBy;
            policy.RevisionCount = 1;

            if (policy.IsPublished && !policy.PublishedAtUtc.HasValue)
            {
                policy.PublishedAtUtc = now;
            }

            policy.ContentHtml = ConvertMarkdownToSafeHtml(policy.ContentMarkdown);

            await _policiesCollection.InsertOneAsync(policy);

            // Record Initial Revision #1
            var revision = new PolicyRevision
            {
                PolicyId = policy.Id ?? string.Empty,
                RevisionNumber = 1,
                Title = policy.Title,
                Slug = policy.Slug,
                Category = policy.Category,
                Summary = policy.Summary,
                ContentMarkdown = policy.ContentMarkdown,
                ContentHtml = policy.ContentHtml,
                ChangedAtUtc = now,
                ChangedBy = policy.CreatedBy,
                ChangeType = policy.IsPublished ? "Published" : "Created",
                Notes = "Initial policy document creation"
            };

            await _revisionsCollection.InsertOneAsync(revision);

            return policy;
        }

        public async Task<PolicyDocument?> UpdatePolicyAsync(PolicyDocument policy, string adminUser, string changeNotes = "")
        {
            if (policy == null || string.IsNullOrWhiteSpace(policy.Id)) return null;

            var existing = await _policiesCollection.Find(p => p.Id == policy.Id && !p.IsDeleted).FirstOrDefaultAsync();
            if (existing == null) return null;

            var now = DateTime.UtcNow;
            var user = string.IsNullOrWhiteSpace(adminUser) ? "SuperAdmin" : adminUser;

            var newSlug = NormalizeSlug(string.IsNullOrWhiteSpace(policy.Slug) ? policy.Title : policy.Slug);
            newSlug = await GenerateUniqueSlugAsync(newSlug, policy.Id);

            int nextRevision = existing.RevisionCount + 1;

            existing.Title = policy.Title;
            existing.Slug = newSlug;
            existing.Category = policy.Category;
            existing.Summary = policy.Summary;
            existing.ContentMarkdown = policy.ContentMarkdown;
            existing.ContentHtml = ConvertMarkdownToSafeHtml(policy.ContentMarkdown);
            existing.EffectiveDate = policy.EffectiveDate;
            existing.UpdatedAtUtc = now;
            existing.LastUpdatedBy = user;
            existing.DisplayOrder = policy.DisplayOrder;
            existing.ShowInFooter = policy.ShowInFooter;
            existing.RevisionCount = nextRevision;

            // Handle publishing state change
            if (!existing.IsPublished && policy.IsPublished)
            {
                existing.IsPublished = true;
                existing.PublishedAtUtc = now;
            }
            else if (existing.IsPublished && !policy.IsPublished)
            {
                existing.IsPublished = false;
            }

            existing.IsActive = policy.IsActive;

            await _policiesCollection.ReplaceOneAsync(p => p.Id == existing.Id, existing);

            // Record Revision History
            var revision = new PolicyRevision
            {
                PolicyId = existing.Id,
                RevisionNumber = nextRevision,
                Title = existing.Title,
                Slug = existing.Slug,
                Category = existing.Category,
                Summary = existing.Summary,
                ContentMarkdown = existing.ContentMarkdown,
                ContentHtml = existing.ContentHtml,
                ChangedAtUtc = now,
                ChangedBy = user,
                ChangeType = string.IsNullOrWhiteSpace(changeNotes) ? "Updated" : changeNotes,
                Notes = changeNotes
            };

            await _revisionsCollection.InsertOneAsync(revision);

            return existing;
        }

        public async Task<bool> TogglePublishAsync(string id, bool publish, string adminUser)
        {
            var existing = await GetByIdAsync(id);
            if (existing == null) return false;

            var now = DateTime.UtcNow;
            var user = string.IsNullOrWhiteSpace(adminUser) ? "SuperAdmin" : adminUser;

            existing.IsPublished = publish;
            if (publish && !existing.PublishedAtUtc.HasValue)
            {
                existing.PublishedAtUtc = now;
            }
            existing.UpdatedAtUtc = now;
            existing.LastUpdatedBy = user;
            existing.RevisionCount++;

            await _policiesCollection.ReplaceOneAsync(p => p.Id == existing.Id, existing);

            await _revisionsCollection.InsertOneAsync(new PolicyRevision
            {
                PolicyId = existing.Id,
                RevisionNumber = existing.RevisionCount,
                Title = existing.Title,
                Slug = existing.Slug,
                Category = existing.Category,
                Summary = existing.Summary,
                ContentMarkdown = existing.ContentMarkdown,
                ContentHtml = existing.ContentHtml,
                ChangedAtUtc = now,
                ChangedBy = user,
                ChangeType = publish ? "Published" : "Unpublished",
                Notes = publish ? "Document published to public website" : "Document unpublished from public view"
            });

            return true;
        }

        public async Task<bool> ToggleActiveAsync(string id, bool active, string adminUser)
        {
            var existing = await GetByIdAsync(id);
            if (existing == null) return false;

            var now = DateTime.UtcNow;
            var user = string.IsNullOrWhiteSpace(adminUser) ? "SuperAdmin" : adminUser;

            existing.IsActive = active;
            existing.UpdatedAtUtc = now;
            existing.LastUpdatedBy = user;
            existing.RevisionCount++;

            await _policiesCollection.ReplaceOneAsync(p => p.Id == existing.Id, existing);

            await _revisionsCollection.InsertOneAsync(new PolicyRevision
            {
                PolicyId = existing.Id,
                RevisionNumber = existing.RevisionCount,
                Title = existing.Title,
                Slug = existing.Slug,
                Category = existing.Category,
                Summary = existing.Summary,
                ContentMarkdown = existing.ContentMarkdown,
                ContentHtml = existing.ContentHtml,
                ChangedAtUtc = now,
                ChangedBy = user,
                ChangeType = active ? "Activated" : "Deactivated",
                Notes = active ? "Policy activated" : "Policy deactivated"
            });

            return true;
        }

        public async Task<bool> DeletePolicyAsync(string id, string adminUser)
        {
            var existing = await GetByIdAsync(id);
            if (existing == null) return false;

            var now = DateTime.UtcNow;
            var user = string.IsNullOrWhiteSpace(adminUser) ? "SuperAdmin" : adminUser;

            // Soft-delete to preserve legal audit records
            existing.IsDeleted = true;
            existing.IsActive = false;
            existing.IsPublished = false;
            existing.UpdatedAtUtc = now;
            existing.LastUpdatedBy = user;
            existing.RevisionCount++;

            await _policiesCollection.ReplaceOneAsync(p => p.Id == existing.Id, existing);

            await _revisionsCollection.InsertOneAsync(new PolicyRevision
            {
                PolicyId = existing.Id,
                RevisionNumber = existing.RevisionCount,
                Title = existing.Title,
                Slug = existing.Slug,
                Category = existing.Category,
                Summary = existing.Summary,
                ContentMarkdown = existing.ContentMarkdown,
                ContentHtml = existing.ContentHtml,
                ChangedAtUtc = now,
                ChangedBy = user,
                ChangeType = "Archived",
                Notes = "Policy soft-deleted/archived by administrator"
            });

            return true;
        }

        public async Task<List<PolicyRevision>> GetRevisionsAsync(string policyId)
        {
            if (string.IsNullOrWhiteSpace(policyId)) return new List<PolicyRevision>();

            return await _revisionsCollection
                .Find(r => r.PolicyId == policyId)
                .SortByDescending(r => r.RevisionNumber)
                .ToListAsync();
        }

        public async Task<PolicyRevision?> GetRevisionByIdAsync(string revisionId)
        {
            if (string.IsNullOrWhiteSpace(revisionId)) return null;
            return await _revisionsCollection.Find(r => r.Id == revisionId).FirstOrDefaultAsync();
        }

        public string ConvertMarkdownToSafeHtml(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

            // 1. Strip raw dangerous HTML tags to prevent XSS
            var safeText = Regex.Replace(markdown, @"<\s*(script|style|iframe|object|embed|applet|form|input|button|meta|link)[^>]*>.*?<\s*/\s*\1\s*>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            safeText = Regex.Replace(safeText, @"<\s*(script|style|iframe|object|embed|applet|form|input|button|meta|link)[^>]*\/?>", "", RegexOptions.IgnoreCase);
            safeText = Regex.Replace(safeText, @"on\w+\s*=\s*(['""]).*?\1", "", RegexOptions.IgnoreCase);
            safeText = Regex.Replace(safeText, @"javascript:\s*", "", RegexOptions.IgnoreCase);

            var lines = safeText.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();
            bool inUl = false;
            bool inOl = false;
            bool inBlockquote = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                var trimmed = line.Trim();

                // Check Horizontal Rule
                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    CloseListAndQuote(sb, ref inUl, ref inOl, ref inBlockquote);
                    sb.AppendLine("<hr class=\"my-4 border-secondary border-opacity-25\" />");
                    continue;
                }

                // Check Headings
                if (trimmed.StartsWith("#### "))
                {
                    CloseListAndQuote(sb, ref inUl, ref inOl, ref inBlockquote);
                    sb.AppendLine($"<h5 class=\"fw-bold fs-6 text-dark mt-4 mb-2\">{FormatInline(trimmed.Substring(5))}</h5>");
                    continue;
                }
                if (trimmed.StartsWith("### "))
                {
                    CloseListAndQuote(sb, ref inUl, ref inOl, ref inBlockquote);
                    sb.AppendLine($"<h4 class=\"fw-bold fs-5 text-dark mt-4 mb-2\">{FormatInline(trimmed.Substring(4))}</h4>");
                    continue;
                }
                if (trimmed.StartsWith("## "))
                {
                    CloseListAndQuote(sb, ref inUl, ref inOl, ref inBlockquote);
                    sb.AppendLine($"<h3 class=\"fw-bold fs-4 text-dark mt-4 mb-3 pb-1 border-bottom\">{FormatInline(trimmed.Substring(3))}</h3>");
                    continue;
                }
                if (trimmed.StartsWith("# "))
                {
                    CloseListAndQuote(sb, ref inUl, ref inOl, ref inBlockquote);
                    sb.AppendLine($"<h2 class=\"fw-bold fs-3 text-dark mt-4 mb-3\">{FormatInline(trimmed.Substring(2))}</h2>");
                    continue;
                }

                // Check Blockquotes
                if (trimmed.StartsWith("> "))
                {
                    if (inUl) { sb.AppendLine("</ul>"); inUl = false; }
                    if (inOl) { sb.AppendLine("</ol>"); inOl = false; }
                    if (!inBlockquote)
                    {
                        sb.AppendLine("<blockquote class=\"border-start border-4 border-primary ps-3 py-1 my-3 bg-light rounded-end fs-8 text-secondary\">");
                        inBlockquote = true;
                    }
                    sb.AppendLine($"<p class=\"mb-1\">{FormatInline(trimmed.Substring(2))}</p>");
                    continue;
                }
                else if (inBlockquote)
                {
                    sb.AppendLine("</blockquote>");
                    inBlockquote = false;
                }

                // Check Unordered List
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    if (inOl) { sb.AppendLine("</ol>"); inOl = false; }
                    if (!inUl)
                    {
                        sb.AppendLine("<ul class=\"mb-3 ps-4\">");
                        inUl = true;
                    }
                    sb.AppendLine($"<li class=\"mb-1\">{FormatInline(trimmed.Substring(2))}</li>");
                    continue;
                }

                // Check Ordered List
                var matchOl = Regex.Match(trimmed, @"^(\d+)\.\s+(.*)$");
                if (matchOl.Success)
                {
                    if (inUl) { sb.AppendLine("</ul>"); inUl = false; }
                    if (!inOl)
                    {
                        sb.AppendLine("<ol class=\"mb-3 ps-4\">");
                        inOl = true;
                    }
                    sb.AppendLine($"<li class=\"mb-1\">{FormatInline(matchOl.Groups[2].Value)}</li>");
                    continue;
                }

                // Normal Paragraph or Empty Line
                CloseListAndQuote(sb, ref inUl, ref inOl, ref inBlockquote);

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                sb.AppendLine($"<p class=\"mb-3 leading-relaxed text-secondary\">{FormatInline(trimmed)}</p>");
            }

            CloseListAndQuote(sb, ref inUl, ref inOl, ref inBlockquote);

            return sb.ToString();
        }

        private static void CloseListAndQuote(StringBuilder sb, ref bool inUl, ref bool inOl, ref bool inBlockquote)
        {
            if (inUl) { sb.AppendLine("</ul>"); inUl = false; }
            if (inOl) { sb.AppendLine("</ol>"); inOl = false; }
            if (inBlockquote) { sb.AppendLine("</blockquote>"); inBlockquote = false; }
        }

        private static string FormatInline(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // HTML encode to prevent tag injections
            var encoded = HttpUtility.HtmlEncode(text);

            // Bold **text**
            encoded = Regex.Replace(encoded, @"\*\*(.+?)\*\*", "<strong class=\"text-dark\">$1</strong>");

            // Italic *text*
            encoded = Regex.Replace(encoded, @"\*(.+?)\*", "<em>$1</em>");

            // Inline Code `text`
            encoded = Regex.Replace(encoded, @"`([^`]+)`", "<code class=\"bg-light px-1 py-0.5 rounded text-dark fs-9 font-mono\">$1</code>");

            // Links [text](url)
            encoded = Regex.Replace(encoded, @"\[([^\]]+)\]\(([^)]+)\)", m =>
            {
                var label = m.Groups[1].Value;
                var url = m.Groups[2].Value.Trim();

                // Validate safe URL schemes
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    return $"<a href=\"{url}\" class=\"text-primary text-decoration-underline\" target=\"_blank\" rel=\"noopener noreferrer\">{label}</a>";
                }

                return label;
            });

            return encoded;
        }

        private static string NormalizeSlug(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "policy";
            var slug = input.Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }

        private async Task<string> GenerateUniqueSlugAsync(string baseSlug, string? currentPolicyId)
        {
            var slug = baseSlug;
            int counter = 1;

            while (true)
            {
                FilterDefinition<PolicyDocument> filter;
                if (!string.IsNullOrWhiteSpace(currentPolicyId))
                {
                    filter = Builders<PolicyDocument>.Filter.And(
                        Builders<PolicyDocument>.Filter.Eq(p => p.Slug, slug),
                        Builders<PolicyDocument>.Filter.Ne(p => p.Id, currentPolicyId),
                        Builders<PolicyDocument>.Filter.Eq(p => p.IsDeleted, false)
                    );
                }
                else
                {
                    filter = Builders<PolicyDocument>.Filter.And(
                        Builders<PolicyDocument>.Filter.Eq(p => p.Slug, slug),
                        Builders<PolicyDocument>.Filter.Eq(p => p.IsDeleted, false)
                    );
                }

                var existing = await _policiesCollection
                    .Find(filter)
                    .FirstOrDefaultAsync();

                if (existing == null) break;

                counter++;
                slug = $"{baseSlug}-{counter}";
            }

            return slug;
        }

        public async Task SeedDefaultPoliciesAsync()
        {
            var count = await _policiesCollection.CountDocumentsAsync(p => !p.IsDeleted);
            if (count > 0) return;

            var defaultPolicies = GetComprehensiveSeedPolicies();
            foreach (var pol in defaultPolicies)
            {
                await CreatePolicyAsync(pol, "System");
            }
        }

        private List<PolicyDocument> GetComprehensiveSeedPolicies()
        {
            var now = DateTime.UtcNow;

            return new List<PolicyDocument>
            {
                // 1. PRIVACY POLICY
                new PolicyDocument
                {
                    Title = "Privacy Policy",
                    Slug = "privacy-policy",
                    Category = "Privacy",
                    Summary = "Information collection, data protection safeguards, user rights under the Digital Personal Data Protection Act (DPDP Act 2023), and privacy practices for SIMS.",
                    DisplayOrder = 1,
                    ShowInFooter = true,
                    IsPublished = true,
                    IsActive = true,
                    EffectiveDate = new DateTime(2026, 1, 1),
                    ContentMarkdown = @"## 1. Introduction and Scope

SIMS provides a cloud-hosted, Software-as-a-Service (SaaS) business management, inventory tracking, point-of-sale (POS) billing, and catalog administration platform.

This Privacy Policy explains how SIMS collects, uses, processes, stores, and safeguards personal data when businesses, merchant administrators, store employees, and visitors access our website and software subscription platform.

We are committed to processing all personal data responsibly, transparently, and in strict adherence to applicable Indian laws, including the **Digital Personal Data Protection Act, 2023 (DPDP Act 2023)**, the **Information Technology Act, 2000**, and the **Information Technology (Reasonable Security Practices and Procedures and Sensitive Personal Data or Information) Rules, 2011**.

---

## 2. Distinction of Roles: Data Fiduciary vs. Data Processor

Under the DPDP Act 2023, data processing roles in our SaaS architecture operate as follows:

* **SIMS as a Data Fiduciary:** We act as a Data Fiduciary regarding the account information, billing details, contact names, business records, and login credentials provided directly by subscribing businesses (""Tenants"") to establish and manage their SIMS software subscriptions.
* **SIMS as a Data Processor:** When a subscribing business records end-customer transactions, customer names, telephone numbers, repair job details, or purchase histories within their private shop tenant database, the subscribing merchant acts as the Data Fiduciary. SIMS acts solely as a technical Data Processor, hosting and safeguarding that data strictly on the merchant's behalf and under their administrative instructions.

---

## 3. Information We Collect

We collect only information that is strictly necessary to deliver, bill, secure, and support our software services:

### A. Information Provided Directly by Subscribing Businesses
* **Merchant Account Details:** Business trading name, authorized representative name, business address, work email address, telephone or WhatsApp contact number, and Goods and Services Tax Identification Number (GSTIN).
* **User Accounts:** Names, designated email addresses or usernames, and role authorizations created by the business administrator for their staff, cashiers, and store managers.
* **Subscription & Billing Records:** Selected subscription package or custom quote capacity, billing cycle, payment transaction identifiers, and invoicing correspondence. (Payment card details and bank credentials are processed directly through authorized payment gateways; SIMS does not store raw credit/debit card numbers).

### B. Technical and Operational Information
* **Authentication and Security Logs:** IP addresses, login timestamps, device browser user agents, and administrative activity logs recorded to detect unauthorized access, protect tenant data isolation, and maintain platform security.
* **Session Cookies:** Essential session identifiers required to maintain secure user login states during active portal usage.

---

## 4. Purpose and Legal Basis for Processing

We process personal data solely for lawful, legitimate business purposes:

1. **Service Delivery:** Providing, maintaining, and authenticating access to the SIMS inventory, billing, and reporting platform.
2. **Subscription Management:** Processing account activations, package renewals, quote requests, tax invoicing, and billing notifications.
3. **Customer Support:** Diagnosing technical inquiries, addressing administrative tickets, and responding to platform service requests.
4. **Security & System Integrity:** Enforcing tenant database isolation, auditing administrative activities, and preventing fraudulent access or abuse.
5. **Legal & Tax Compliance:** Complying with applicable Indian statutory obligations, including tax record retention under GST laws and responding to lawful requests by competent legal authorities.

---

## 5. Information Sharing and Disclosure

SIMS does not sell, rent, lease, or commercially trade personal information to any third parties for advertising or marketing purposes.

We share data only in the following limited, legally justified circumstances:

* **Authorized Service Providers:** We engage trusted operational partners strictly necessary for cloud data hosting, transactional communications, and authorized payment processing. These entities process information solely under contractual confidentiality and data security obligations.
* **Statutory and Legal Mandates:** We may disclose specific records if required to do so by applicable Indian law, court order, or formal directive issued by an authorized government, law enforcement, or regulatory body.
* **Business Reorganization:** In the event of a merger, acquisition, corporate restructuring, or asset transfer, customer records may be transferred subject to the same privacy commitments established in this Policy.

---

## 6. Data Security and Safeguards

SIMS implements comprehensive organizational, physical, and technical measures designed to protect information from unauthorized access, accidental alteration, disclosure, or destruction:

* Role-based tenant isolation ensuring that one merchant's business and customer records cannot be accessed by another subscriber.
* Encryption of data in transit across public networks using standard cryptographic protocols.
* Salted cryptographic hashing for user login passwords.
* Multi-factor verification and audit trails for privileged administrative actions.

---

## 7. Data Retention and Deletion

* **Active Subscriptions:** Account records and business data are retained throughout the active software subscription term.
* **Post-Expiry Retention:** Following subscription expiry or account closure, data is retained for a reasonable grace period (typically 30 to 90 days) to permit account renewal or authorized data export.
* **Statutory Invoicing Records:** Invoices, tax records, and transactional payment references are retained for the mandatory statutory period mandated by Indian tax and accounting regulations.
* **Data Erasure:** Upon formal request or after expiration of required retention periods, personal data is permanently deleted or anonymized from active operational databases.

---

## 8. Rights of Data Principals

In accordance with the DPDP Act 2023, Data Principals possess specific statutory rights regarding their personal data:

* **Right to Access:** Request a summary of personal data held and processed by SIMS as a Data Fiduciary.
* **Right to Correction and Updating:** Request the correction, completion, or updating of inaccurate or outdated account records.
* **Right to Erasure:** Request the deletion of personal data where continuing retention is no longer necessary for service delivery or statutory compliance.
* **Right to Grievance Redressal:** Submit inquiries or complaints regarding data handling to our designated Grievance Officer.

To exercise these rights, please contact our administrative desk using the contact details provided below.

---

## 9. Software-Only Operational Scope

SIMS is strictly a software subscription service. SIMS does not sell, supply, repair, or warrant physical mobile phones, laptops, or hardware items. Any customer records, device serials, or IMEI numbers tracked within the software are entered solely by the merchant for their own shop records.

---

## 10. Grievance Redressal Officer Contact

In compliance with the Information Technology Act, 2000 and the DPDP Act, 2023, our designated Grievance Officer details are as follows:

* **Grievance Officer:** Legal & Compliance Desk
* **Platform:** SIMS Smart Inventory Management System
* **Email:** support@sims.com
* **Registered Address:** 123 Business Hub, Mumbai, India
* **Response Window:** Inquiries and privacy grievances will be acknowledged within 48 business hours and redressed in accordance with applicable statutory timelines."
                },

                // 2. TERMS AND CONDITIONS
                new PolicyDocument
                {
                    Title = "Terms and Conditions",
                    Slug = "terms-and-conditions",
                    Category = "Terms and Conditions",
                    Summary = "Commercial agreement, subscription terms, acceptable use covenants, intellectual property rights, and liability terms for SIMS SaaS platform.",
                    DisplayOrder = 2,
                    ShowInFooter = true,
                    IsPublished = true,
                    IsActive = true,
                    EffectiveDate = new DateTime(2026, 1, 1),
                    ContentMarkdown = @"## 1. Agreement to Terms

These Terms and Conditions (""Terms"") constitute a legally binding commercial agreement between SIMS (""Platform"", ""we"", ""us"", or ""our"") and the business entity or individual subscribing to or accessing our online service (""Subscriber"", ""Merchant"", ""Customer"", or ""You"").

By registering an account, starting a trial, purchasing a subscription package, or accessing the SIMS portal, you acknowledge that you have read, understood, and agreed to be bound by these Terms and our Privacy Policy.

If you are entering into these Terms on behalf of a company, partnership, or trade entity, you represent and warrant that you hold full legal authority to bind that entity.

---

## 2. Software-Only SaaS Service Description

SIMS is an online cloud-based Software-as-a-Service (SaaS) application providing shop inventory records, point-of-sale (POS) billing tools, trade catalog administration, repair job logging, vendor ledgers, and operational analytics.

### Clear Disclaimer on Physical Hardware:
* SIMS is strictly a software provider. We do **not** sell, manufacture, distribute, repair, install, replace, or warrant physical mobile phones, laptops, electronics, replacement components, or workshop equipment.
* Software features that permit subscribers to record IMEI numbers, serial units, warranty dates, supplier details, or repair status tickets are recording tools used solely by the merchant to manage their own business records.
* SIMS assumes no responsibility or liability for physical products sold, purchased, or repaired by subscribers, nor for any customer disputes arising from physical goods handled in subscribers' shops.

---

## 3. Account Registration and Tenant Responsibility

* **Accurate Information:** Subscribers must provide truthful, current, and complete business information during registration and keep account profiles updated.
* **Credential Confidentiality:** The subscriber is solely responsible for maintaining the confidentiality of administrative and staff login credentials. Any activity conducted under a subscriber's authorized accounts is deemed the responsibility of the subscriber.
* **Notification of Breach:** You must promptly notify SIMS of any suspected unauthorized access, credential compromise, or security incident affecting your account.

---

## 4. Subscription Plans, Billing, and Taxes

* **Subscription Fees:** Access to SIMS is provided on a recurring subscription basis (monthly or annual billing) in accordance with the package selected or custom quote confirmed.
* **Package Capacity Limits:** Each subscription package specifies authorized allocations (such as number of administrator accounts, staff seats, catalog product limits, and supplier accounts). Exceeding configured limits requires an upgrade to a higher tier or custom package amendment.
* **Estimator Calculator:** Pricing figures calculated on the public custom package estimator represent planning estimates. Formal binding pricing is finalized upon administrative confirmation or payment checkout.
* **Taxes and Invoicing:** Subscription charges are subject to applicable Indian Goods and Services Tax (GST). Valid GST tax invoices are provided to registered subscribers upon receipt of payment. Subscribers are responsible for providing an accurate GSTIN.

---

## 5. Acceptable Use and Prohibited Conduct

Subscribers agree to use SIMS exclusively for lawful commercial inventory and retail management. You agree that you will not:

1. Use the platform for any fraudulent, unlawful, deceptive, or prohibited trade purpose.
2. Enter, store, or transmit records relating to stolen, counterfeit, or illicit goods, or violate any applicable Indian law.
3. Attempt to bypass, tamper with, or circumvent tenant database isolation, security controls, or licensing parameters.
4. Reverse engineer, decompile, disassemble, or extract source code or proprietary software logic from SIMS.
5. Introduce viruses, worms, Trojan horses, or harmful code designed to disrupt platform integrity.
6. Resell, sublicense, lease, or provide SIMS as a bureau service to third parties without prior written authorization.

---

## 6. Intellectual Property Rights

* **SIMS Platform Ownership:** All rights, title, and interest in and to the SIMS software, source code, user interfaces, documentation, trade dress, trademarks, logos, and algorithms remain the exclusive intellectual property of SIMS.
* **Subscriber Data Ownership:** Subscribers retain complete and unencumbered ownership of all commercial business data, trade inventory records, customer lists, and financial figures uploaded or entered into their private tenant database.
* **Limited License:** Subject to active subscription payment, SIMS grants the subscriber a non-exclusive, non-transferable, revocable license to access and use the platform strictly for internal business operations during the subscription term.

---

## 7. Copyright and Content Complaint Procedure (Indian Copyright Act, 1957)

Subscribers warrant that they hold the legal right to upload and display any trademarks, catalog images, product descriptions, or logos entered into SIMS.

If any copyright holder believes that material hosted within SIMS infringes their copyright under the **Indian Copyright Act, 1957**, they may submit a formal complaint to our legal desk with the following details:

1. Identification of the copyrighted work claimed to have been infringed.
2. Specific URL, account, or location where the alleged infringing material resides.
3. Proof of copyright ownership or legal authorization to act on behalf of the owner.
4. Contact details of the complainant (full legal name, physical address, telephone number, and official email).
5. A signed statement of good-faith belief that the use is unauthorized.

**Review Process:** Upon receipt of a valid statutory notice, SIMS will review the claim, notify the affected subscriber, and take appropriate action—including restricting access to infringing material where legally justified. The affected subscriber retains the right to submit a formal counter-notice in accordance with applicable law.

---

## 8. Service Availability and Maintenance

* **Operational Availability:** SIMS uses reasonable commercial efforts to ensure reliable platform availability. However, continuous, uninterrupted access cannot be guaranteed.
* **Scheduled Maintenance:** Periodic maintenance, software enhancements, and security upgrades may require temporary downtime. Where feasible, advance notice of scheduled maintenance will be posted on the portal.
* **Service Interruption:** SIMS is not liable for service interruptions caused by telecommunication outages, third-party internet service provider disruptions, or force majeure events.

---

## 9. Data Export, Account Expiry, and Termination

* **Voluntary Cancellation:** Subscribers may terminate their subscription at any time through their administrative control center or by written notice.
* **Data Export:** Prior to subscription expiry, subscribers may export their product lists, invoice records, and supplier ledgers using built-in export features.
* **Termination for Cause:** SIMS reserves the right to suspend or terminate accounts without prior notice in cases of serious acceptable-use violations, fraudulent activity, prolonged non-payment, or valid legal directive.

---

## 10. Limitation of Liability and Disclaimers

* **As-Is Provision:** SIMS is provided on an ""as is"" and ""as available"" basis without express or implied warranties of merchantability, fitness for a particular trade purpose, or zero-defect performance.
* **Indirect Losses:** To the maximum extent permitted by applicable Indian law, SIMS shall not be liable to any subscriber or third party for indirect, incidental, consequential, special, or punitive damages, including loss of profits, commercial interruption, loss of business records, or loss of anticipated savings.
* **Liability Cap:** In all circumstances, SIMS's aggregate cumulative liability arising under or relating to these Terms shall not exceed the total subscription fees actually paid by the subscriber to SIMS in the three (3) months immediately preceding the claim.

---

## 11. Governing Law and Dispute Resolution

* **Governing Law:** These Terms, and all claims or disputes arising out of or related hereto, shall be governed by and construed in accordance with the laws of the **Republic of India**, without regard to conflict of law principles.
* **Amicable Settlement:** In the event of any dispute, controversy, or claim, the parties agree to first attempt resolution through good-faith mutual discussions for thirty (30) business days.
* **Jurisdiction:** If amicable settlement is not achieved, the competent courts having jurisdiction over Mumbai, Maharashtra, India shall have exclusive territorial jurisdiction.

---

## 12. Contact Information

For commercial inquiries, account support, or legal notices regarding these Terms:

* **Entity:** SIMS SaaS Platform Operations
* **Support Email:** support@sims.com
* **Registered Address:** 123 Business Hub, Mumbai, India"
                },

                // 3. REFUND AND CANCELLATION POLICY
                new PolicyDocument
                {
                    Title = "Refund and Cancellation Policy",
                    Slug = "refund-policy",
                    Category = "Refund and Cancellation",
                    Summary = "Clear, fair cancellation procedures, subscription refund terms, trial periods, and payment correction guidelines for SIMS SaaS.",
                    DisplayOrder = 3,
                    ShowInFooter = true,
                    IsPublished = true,
                    IsActive = true,
                    EffectiveDate = new DateTime(2026, 1, 1),
                    ContentMarkdown = @"## 1. Overview and Philosophy

At SIMS, we believe in fair, transparent, and predictable subscription terms.

This Refund and Cancellation Policy explains how subscription cancellations, renewals, billing disputes, duplicate payment corrections, and refund requests are handled across our software platform.

As a B2B Software-as-a-Service (SaaS) provider, our costs are directly tied to computational provisioning, database tenancy allocation, and infrastructure maintenance from the moment a subscription is initiated.

---

## 2. 14-Day Free Evaluation Trial

To ensure that SIMS satisfies your business operational needs before any financial commitment:

* We offer a **14-day fully featured free evaluation trial** on our standard SaaS packages.
* During the trial period, merchants have complete access to test POS billing, inventory tracking, staff user controls, and reporting tools.
* **No automatic billing occurs during the trial.** You are only charged if you proactively choose to activate a paid commercial subscription after evaluating the software.

---

## 3. Subscription Cancellation Procedure

* **Cancellation Anytime:** You may cancel your SIMS software subscription at any time through the administrative settings panel or by emailing support@sims.com from your registered account email.
* **Effect of Cancellation:** When you cancel your subscription, automatic renewal is immediately discontinued.
* **Access Through Term:** Your account and team access will remain fully functional until the end of the currently active paid billing period (month or year).
* **No Cancellation Penalties:** SIMS charges zero cancellation fees or account termination penalties.

---

## 4. Refund Eligibility Guidelines

Refund requests are evaluated fairly in accordance with the following criteria:

### A. Monthly Subscriptions
* Monthly subscriptions are non-refundable once the billing cycle has begun, because server infrastructure, license provisioning, and platform maintenance are committed for that monthly cycle.
* Cancellations take effect at the conclusion of the active monthly term.

### B. Annual Subscriptions
* If an annual subscription is cancelled within **fourteen (14) calendar days** of the initial annual purchase date, you may request a refund. In such cases, a prorated refund will be issued, deducting only the standard single-month non-discounted rate for the utilized month and applicable payment gateway processing charges.
* After fourteen (14) calendar days from the annual payment date, annual subscription fees are considered fully committed and non-refundable.

### C. Duplicate or Erroneous Payments
* In the event of a technical error resulting in duplicate charges or erroneous multiple debits for the same subscription period, SIMS will verify the transaction logs and issue a **100% full refund** for the duplicate amount.
* Duplicate charge refund claims must be reported to our billing support desk within thirty (30) days of the transaction date.

### D. Substantial Service Non-Delivery
* If SIMS experiences severe, prolonged, unscheduled service downtime exceeding 72 consecutive hours attributable directly to our internal infrastructure failures, affected active subscribers may request a prorated credit or refund for the affected period.

---

## 5. Non-Refundable Circumstances

Refunds will not be issued in the following scenarios:

1. Partial utilization of configured quota (e.g., subscribing to a 10,000 product package but uploading only 2,000 products).
2. Failure to use the platform during an active, paid subscription period due to subscriber's internal business decisions or personnel changes.
3. Suspension or termination of an account resulting from verified violations of our Acceptable Use Policy, fraudulent activity, or illegal conduct.
4. Physical device, hardware, or third-party equipment failures (SIMS is a software service and does not guarantee or refund third-party barcode scanners, receipt printers, or mobile phones).

---

## 6. How to Submit a Refund or Billing Inquiry

To submit a billing dispute or request a refund review:

1. Email our billing department at **support@sims.com** from your registered business email address.
2. Include the following details:
   * Registered Business Name and Tenant Account ID.
   * Transaction Date and Payment Reference / Gateway Transaction ID.
   * Specific reason for the refund or billing adjustment request.
3. **Review Timeline:** Our billing team will examine your account records, verify payment gateway logs, and respond within **five (5) business days**.
4. **Disbursement:** Approved refunds are credited back to the original payment source (bank account, UPI, or card) within **5 to 7 business days**, in accordance with standard Indian banking settlement cycles.

---

## 7. Contact Us

For any billing, renewal, or cancellation inquiries:

* **Billing Desk:** SIMS SaaS Platform Billing Support
* **Email:** support@sims.com
* **Support Hours:** Monday – Saturday, 9:00 AM – 8:00 PM IST"
                },

                // 4. SUBSCRIPTION AND BILLING POLICY
                new PolicyDocument
                {
                    Title = "Subscription and Billing Policy",
                    Slug = "subscription-policy",
                    Category = "Subscription and Billing",
                    Summary = "Billing cycles, payment methods, automatic renewal terms, package limits, custom calculator pricing estimates, and GST tax invoicing rules.",
                    DisplayOrder = 4,
                    ShowInFooter = true,
                    IsPublished = true,
                    IsActive = true,
                    EffectiveDate = new DateTime(2026, 1, 1),
                    ContentMarkdown = @"## 1. Overview

This Subscription and Billing Policy describes the commercial terms, payment schedules, capacity allocations, invoice procedures, and renewal mechanisms governing paid subscriptions to the SIMS software platform.

All transactions and fees are denominated in Indian Rupees (INR / ₹) unless explicitly stated otherwise.

---

## 2. Billing Cycles: Monthly vs. Annual

Subscribers may choose between two billing options:

* **Monthly Billing:** Subscriptions renew every thirty (30) days. Payment is due in advance at the start of each monthly cycle.
* **Annual Billing:** Subscriptions are paid for a full twelve (12) month term in advance. Annual plans benefit from configured volume discounts (e.g., 20% discount compared to monthly rates).

---

## 3. Package Capacity and Limit Enforcement

SIMS subscriptions are scaled to match your business operational capacity:

* **Configured Allocations:** Each package defines specific limits for:
  * Number of Shop Administrators.
  * Number of Store Employees / Staff / Cashiers.
  * Maximum Inventory Catalog Products.
  * Registered Wholesale Suppliers.
* **All Modules Included:** Custom package calculations include access to all standard and advanced SIMS modules (IMEI tracking, POS billing, repair job tracking, trade-in exchange, invoice generation, and audit logs) without extra per-module surcharge.
* **Approaching Limits:** When your business reaches 90% of a configured limit (e.g. product catalog count), the administrative portal will display an advisory banner.
* **Exceeding Limits:** If limits are reached, the creation of additional records in that category will be paused until the administrator upgrades the subscription tier or removes obsolete records.

---

## 4. Custom Calculator Estimates

* The public **Custom Package Calculator** provides real-time indicative pricing estimates based on Super Admin configured rates.
* Figures shown by the online calculator are **estimates for operational planning** and do not constitute a confirmed final commercial invoice.
* Final binding quotations and customized onboarding agreements are verified upon formal quote review and payment checkout.

---

## 5. Payment Methods and Processing

* We accept standard digital payment methods through authorized Indian payment gateway providers, including:
  * Unified Payments Interface (UPI).
  * Net Banking across major Indian banks.
  * Corporate and Commercial Credit / Debit Cards (Visa, MasterCard, RuPay).
  * Authorized Electronic Standing Instructions (e-Mandate) for automated renewals where supported.
* **Security:** Payment transactions are encrypted and processed through RBI-authorized payment aggregators. SIMS does not collect, handle, or store sensitive card verification codes (CVV) or bank account passwords.

---

## 6. Taxes and Invoicing (GST Compliance)

* **Goods and Services Tax (GST):** Subscription fees are subject to statutory Indian GST (currently 18% IGST or CGST + SGST as applicable).
* **Tax Invoices:** A digitally signed, GST-compliant tax invoice will be generated and made available in your account control center immediately following successful payment.
* **GSTIN Accuracy:** Subscribers claiming Input Tax Credit (ITC) must ensure their valid GSTIN and legal trade name are accurately entered in their platform business profile prior to invoice generation.

---

## 7. Renewal, Expiry, and Grace Periods

* **Renewal Notices:** Automated email notifications are sent 7 days and 2 days prior to scheduled subscription renewal or expiry.
* **Unsuccessful Payments:** In the event of an unsuccessful payment or expired mandate, SIMS provides a **five (5) calendar day grace period** during which platform access remains active while you update your payment source.
* **Suspension:** If renewal is not completed following the grace period, administrative and staff access will be temporarily suspended. Tenant data is safely preserved during suspension.
* **Data Retention Post-Expiry:** Unrenewed tenant records are preserved for a minimum of thirty (30) days following suspension to enable data recovery upon renewal.

---

## 8. Billing Inquiries and Support

For questions regarding invoices, package sizing, custom quotes, or payment receipts:

* **Email:** support@sims.com
* **Help Desk:** Accessible directly from your Shop Admin dashboard under Settings → Billing."
                },

                // 5. COOKIE POLICY
                new PolicyDocument
                {
                    Title = "Cookie Policy",
                    Slug = "cookie-policy",
                    Category = "Cookies",
                    Summary = "Transparent explanation of essential cookies and session security technologies utilized across the SIMS platform.",
                    DisplayOrder = 5,
                    ShowInFooter = true,
                    IsPublished = true,
                    IsActive = true,
                    EffectiveDate = new DateTime(2026, 1, 1),
                    ContentMarkdown = @"## 1. Introduction

This Cookie Policy explains how SIMS uses cookies and similar browser storage technologies across our public website and SaaS operational portals.

We maintain a strict, privacy-first approach to web technologies: **SIMS uses cookies exclusively for essential technical functionality, session security, and account authentication.** We do not deploy third-party advertising cookies, cross-site behavioral tracking scripts, or commercial data-broker pixels.

---

## 2. What Are Cookies?

Cookies are small text files placed on your computer, tablet, or mobile device by websites that you visit. They are widely used to make websites function efficiently, remember active login sessions, and protect account credentials from unauthorized interception.

---

## 3. Categories of Cookies We Use

SIMS uses only the following strictly necessary categories of cookies:

### A. Strictly Necessary Authentication Cookies
* **Purpose:** These cookies identify your unique encrypted session when you sign in to your Shop Admin or Super Admin portal.
* **Why Needed:** Without these cookies, the portal cannot verify that you are authorized to view your store's inventory and billing data as you navigate between pages.
* **Duration:** Session cookies expire automatically when you log out or close your web browser.

### B. Security and Anti-Tampering Cookies (Anti-CSRF)
* **Purpose:** These tokens protect against Cross-Site Request Forgery (CSRF) attacks by ensuring that form submissions (such as saving a product or creating an invoice) originate authentically from your logged-in browser session.
* **Why Needed:** Crucial for maintaining database integrity and preventing malicious external scripts from executing unauthorized commands.

### C. Operational UI Preferences (Local Storage)
* **Purpose:** Minimal browser local storage keys may record user interface preferences, such as whether your sidebar navigation menu is expanded or collapsed, or billing cycle selection on the pricing page.
* **Why Needed:** Enhances your administrative workflow experience by remembering your display layout.

---

## 4. What We Do NOT Do

To protect your privacy and business confidentiality:

* We do **not** use third-party behavioral advertising or retargeting cookies.
* We do **not** sell, license, or disclose your browsing behavior to data brokers or ad networks.
* We do **not** track your online activity across websites you visit outside of SIMS.

---

## 5. Managing Your Browser Cookie Settings

Most web browsers permit you to manage or block cookies through browser preferences. You can configure your browser to reject all cookies or notify you when a cookie is placed.

However, because the cookies used by SIMS are strictly necessary for secure authentication and tenant isolation, blocking or deleting essential cookies will prevent you from signing in to your administrative portal and utilizing the software.

---

## 6. Updates to This Cookie Policy

We may update this Cookie Policy occasionally to reflect technical improvements or regulatory changes under Indian data protection laws. Any updates will be published on this page with an updated timestamp.

---

## 7. Contact Us

If you have questions regarding our use of cookies or technical privacy safeguards:

* **Email:** support@sims.com
* **Postal Address:** 123 Business Hub, Mumbai, India"
                }
            };
        }
    }
}
