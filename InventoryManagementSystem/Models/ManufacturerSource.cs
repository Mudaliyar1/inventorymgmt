using System.Collections.Generic;

namespace InventoryManagementSystem.Models
{
    public class ManufacturerSource
    {
        public string Brand { get; set; } = string.Empty;
        public string OfficialDomain { get; set; } = string.Empty;
        public List<string> RegionalDomains { get; set; } = new List<string>();
        public string SearchPattern { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }
}
