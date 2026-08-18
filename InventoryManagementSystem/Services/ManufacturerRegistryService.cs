using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Services
{
    public class ManufacturerRegistryService : IManufacturerRegistryService
    {
        private readonly ConcurrentDictionary<string, ManufacturerSource> _sources = new(StringComparer.OrdinalIgnoreCase);

        public ManufacturerRegistryService()
        {
            InitializeDefaultRegistry();
        }

        private void InitializeDefaultRegistry()
        {
            var defaultSources = new List<ManufacturerSource>
            {
                new ManufacturerSource
                {
                    Brand = "vivo",
                    OfficialDomain = "vivo.com",
                    RegionalDomains = new List<string> { "vivo.com/in" },
                    SearchPattern = "site:vivo.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Apple",
                    OfficialDomain = "apple.com",
                    RegionalDomains = new List<string> { "apple.com/in" },
                    SearchPattern = "site:apple.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Samsung",
                    OfficialDomain = "samsung.com",
                    RegionalDomains = new List<string> { "samsung.com/in" },
                    SearchPattern = "site:samsung.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Xiaomi",
                    OfficialDomain = "mi.com",
                    RegionalDomains = new List<string> { "mi.com/in", "po.co/in" },
                    SearchPattern = "site:mi.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Poco",
                    OfficialDomain = "po.co",
                    RegionalDomains = new List<string> { "po.co/in", "mi.com/in" },
                    SearchPattern = "site:po.co/in"
                },
                new ManufacturerSource
                {
                    Brand = "OnePlus",
                    OfficialDomain = "oneplus.com",
                    RegionalDomains = new List<string> { "oneplus.in", "oneplus.com/in" },
                    SearchPattern = "site:oneplus.in"
                },
                new ManufacturerSource
                {
                    Brand = "Motorola",
                    OfficialDomain = "motorola.com",
                    RegionalDomains = new List<string> { "motorola.in", "motorola.com/in" },
                    SearchPattern = "site:motorola.in"
                },
                new ManufacturerSource
                {
                    Brand = "OPPO",
                    OfficialDomain = "oppo.com",
                    RegionalDomains = new List<string> { "oppo.com/in" },
                    SearchPattern = "site:oppo.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Realme",
                    OfficialDomain = "realme.com",
                    RegionalDomains = new List<string> { "realme.com/in" },
                    SearchPattern = "site:realme.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Google",
                    OfficialDomain = "store.google.com",
                    RegionalDomains = new List<string> { "store.google.com/in" },
                    SearchPattern = "site:store.google.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Nothing",
                    OfficialDomain = "nothing.tech",
                    RegionalDomains = new List<string> { "in.nothing.tech" },
                    SearchPattern = "site:nothing.tech"
                },
                new ManufacturerSource
                {
                    Brand = "Honor",
                    OfficialDomain = "hihonor.com",
                    RegionalDomains = new List<string> { "hihonor.com/in" },
                    SearchPattern = "site:hihonor.com"
                },
                new ManufacturerSource
                {
                    Brand = "ASUS",
                    OfficialDomain = "asus.com",
                    RegionalDomains = new List<string> { "asus.com/in" },
                    SearchPattern = "site:asus.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Sony",
                    OfficialDomain = "sony.com",
                    RegionalDomains = new List<string> { "sony.co.in" },
                    SearchPattern = "site:sony.co.in"
                },
                new ManufacturerSource
                {
                    Brand = "HMD",
                    OfficialDomain = "hmd.com",
                    RegionalDomains = new List<string> { "hmd.com/in" },
                    SearchPattern = "site:hmd.com/in"
                },
                new ManufacturerSource
                {
                    Brand = "Nokia",
                    OfficialDomain = "hmd.com",
                    RegionalDomains = new List<string> { "hmd.com/in", "nokia.com" },
                    SearchPattern = "site:hmd.com/in"
                }
            };

            foreach (var src in defaultSources)
            {
                _sources[src.Brand] = src;
            }
        }

        public IEnumerable<ManufacturerSource> GetAllSources()
        {
            return _sources.Values.Where(s => s.Active);
        }

        public ManufacturerSource? GetSourceForBrand(string brand)
        {
            if (string.IsNullOrWhiteSpace(brand)) return null;

            brand = brand.Trim();
            if (_sources.TryGetValue(brand, out var exact))
            {
                return exact;
            }

            // Partial match fallback
            return _sources.Values.FirstOrDefault(s => s.Active && (
                s.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase) ||
                brand.Contains(s.Brand, StringComparison.OrdinalIgnoreCase) ||
                s.Brand.Contains(brand, StringComparison.OrdinalIgnoreCase)));
        }

        public void RegisterSource(ManufacturerSource source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Brand)) return;
            _sources[source.Brand.Trim()] = source;
        }
    }
}
