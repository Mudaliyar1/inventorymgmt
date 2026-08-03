using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Repositories;
using InventoryManagementSystem.Services;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== STARTING PRODUCT EDIT DIAGNOSTIC TEST ===");
        
        // Load env variables
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                var parts = trimmedLine.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }

        var connString = Environment.GetEnvironmentVariable("MongoDbSettings__ConnectionString");
        Console.WriteLine($"ConnectionString loaded: {(!string.IsNullOrEmpty(connString))}");

        // Set up Mongo Context
        var mongoSettings = Options.Create(new MongoDbSettings
        {
            ConnectionString = connString,
            DatabaseName = "SIMS_Db"
        });
        var context = new MongoDbContext(mongoSettings);

        // Set up Repositories and Services
        var productRepo = new ProductRepository(context);
        var productService = new ProductService(productRepo);

        // Fetch first product
        var products = await productService.GetAllProductsAsync();
        var product = products.FirstOrDefault();

        if (product == null)
        {
            Console.WriteLine("No products found in database. Creating a new test product...");
            product = new Product
            {
                Name = "Test Diagnostic Product",
                Code = "TEST-DIAG",
                Barcode = "1234567890",
                CategoryId = "60c72b2f9b1d8e1f2c3d4e5f",
                PurchasePrice = 10.0m,
                SellingPrice = 15.0m,
                CurrentStock = 100,
                MinimumStock = 10,
                Description = "Original Description",
                Status = "Active"
            };
            await productService.CreateProductAsync(product);
            Console.WriteLine($"Created test product with ID: {product.Id}");
        }
        else
        {
            Console.WriteLine($"Fetched existing product: '{product.Name}' (ID: {product.Id})");
        }

        // Try updating
        Console.WriteLine("\n--- Simulating Update ---");
        var originalDescription = product.Description;
        product.Description = originalDescription + " - EDITED AT " + DateTime.UtcNow.ToString();
        product.PurchasePrice += 1.0m;

        Console.WriteLine($"Updating product ID {product.Id}. Description: '{product.Description}', Price: {product.PurchasePrice}");
        try
        {
            await productService.UpdateProductAsync(product);
            Console.WriteLine("UpdateProductAsync finished without exceptions.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateProductAsync THREW AN EXCEPTION: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        // Verify result
        Console.WriteLine("\n--- Verifying Update ---");
        var updatedProduct = await productService.GetProductByIdAsync(product.Id);
        if (updatedProduct == null)
        {
            Console.WriteLine("Verification failed: Product not found by ID after update!");
        }
        else
        {
            Console.WriteLine($"Verification result: Name='{updatedProduct.Name}', Description='{updatedProduct.Description}', Price={updatedProduct.PurchasePrice}");
            if (updatedProduct.Description == product.Description)
            {
                Console.WriteLine("SUCCESS! Changes saved successfully to MongoDB Atlas!");
            }
            else
            {
                Console.WriteLine("FAILURE! Changes were NOT saved. Database holds original values.");
            }
        }
    }
}
