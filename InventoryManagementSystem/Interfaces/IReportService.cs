using InventoryManagementSystem.Models;
using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Interfaces
{
    public interface IReportService
    {
        byte[] GenerateSalesExcelReport(DateTime start, DateTime end, IEnumerable<Sale> sales);
        byte[] GenerateInventoryValuationExcelReport(IEnumerable<Product> products, Dictionary<string, string> categoryNames);
        byte[] GenerateInventoryPdfReport(IEnumerable<Product> products, Dictionary<string, string> categoryNames);
    }
}
