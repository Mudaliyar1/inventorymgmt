using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.DTOs
{
    public class SpecSourceItem
    {
        public string SiteName { get; set; } = string.Empty;
        public string PageTitle { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string SourceType { get; set; } = "Official Manufacturer"; // "Official Manufacturer" or "Third-Party"
        public DateTime RetrievedDate { get; set; } = DateTime.UtcNow;
    }

    public class MobileSpecSearchResultDto
    {
        public bool Success { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;

        public string PrimarySourceType { get; set; } = "Official Manufacturer"; // "Official Manufacturer" or "Third-Party"
        public string OfficialDomain { get; set; } = string.Empty;
        public bool ExactModelMatched { get; set; } = false;
        public List<string> AvailableVariants { get; set; } = new List<string>();

        public List<SpecSourceItem> SourceWebsites { get; set; } = new List<SpecSourceItem>();
        public string ConfidenceMatch { get; set; } = "Needs Verification"; // High, Needs Verification, Low
        public string ConfidenceReason { get; set; } = string.Empty;
        public bool AmbiguousVariantsFound { get; set; } = false;
        public bool RequiresBrowserRendering { get; set; } = false;

        // Extraction Statistics
        public int TotalFieldsCount { get; set; } = 44;
        public int PopulatedFieldsCount { get; set; } = 0;
        public int OfficialFieldsCount { get; set; } = 0;
        public int ThirdPartyFieldsCount { get; set; } = 0;
        public int UnavailableFieldsCount { get; set; } = 44;

        public MobileSpecifications ExtractedSpecs { get; set; } = new MobileSpecifications();

        // Maps specification field names (e.g., ProcessorName, BatteryCapacityMah) to source tags e.g. "Official"
        public Dictionary<string, string> FieldSources { get; set; } = new Dictionary<string, string>();
    }
}
