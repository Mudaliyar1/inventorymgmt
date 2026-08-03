using DotNetEnv;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Repositories;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.Middleware;
using InventoryManagementSystem.Extensions;

// Load .env automatically (supports root workspace, current directory, and bin output dirs)
try
{
    Env.TraversePath().Load();
}
catch (Exception ex)
{
    Console.WriteLine($"[Config] DotNetEnv load notice: {ex.Message}");
}

var builder = WebApplication.CreateBuilder(args);

// Configure resolution order: .env (EnvVars) -> UserSecrets -> appsettings.Development.json -> appsettings.json
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Configuration.AddEnvironmentVariables();

// Helper method to resolve settings with fallback priority
string GetConfigValue(string envKey, string configKey, string defaultValue = "")
{
    var val = Environment.GetEnvironmentVariable(envKey);
    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();

    val = builder.Configuration[configKey];
    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();

    val = builder.Configuration[envKey];
    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();

    return defaultValue;
}

// Resolve strongly-typed setting objects
var mongoSettings = new MongoDbSettings
{
    ConnectionString = GetConfigValue("MONGODB_CONNECTION_STRING", "MongoDbSettings:ConnectionString"),
    DatabaseName = GetConfigValue("MONGODB_DATABASE", "MongoDbSettings:DatabaseName", "SIMS_Db")
};

var cloudinarySettings = new CloudinarySettings
{
    CloudName = GetConfigValue("CLOUDINARY_CLOUD_NAME", "CloudinarySettings:CloudName"),
    ApiKey = GetConfigValue("CLOUDINARY_API_KEY", "CloudinarySettings:ApiKey"),
    ApiSecret = GetConfigValue("CLOUDINARY_API_SECRET", "CloudinarySettings:ApiSecret")
};

int.TryParse(GetConfigValue("BREVO_PORT", "BrevoSettings:Port", "587"), out int brevoPort);
var brevoSettings = new BrevoSettings
{
    Host = GetConfigValue("BREVO_HOST", "BrevoSettings:Host", "smtp-relay.brevo.com"),
    Port = brevoPort > 0 ? brevoPort : 587,
    Username = GetConfigValue("BREVO_USERNAME", "BrevoSettings:Username"),
    Password = GetConfigValue("BREVO_PASSWORD", "BrevoSettings:Password"),
    FromEmail = GetConfigValue("BREVO_FROM_EMAIL", "BrevoSettings:FromEmail"),
    FromName = GetConfigValue("BREVO_FROM_NAME", "BrevoSettings:FromName", "SIMS System")
};

// Register strongly-typed IOptions<T> instances
builder.Services.Configure<MongoDbSettings>(options =>
{
    options.ConnectionString = mongoSettings.ConnectionString;
    options.DatabaseName = mongoSettings.DatabaseName;
});

builder.Services.Configure<CloudinarySettings>(options =>
{
    options.CloudName = cloudinarySettings.CloudName;
    options.ApiKey = cloudinarySettings.ApiKey;
    options.ApiSecret = cloudinarySettings.ApiSecret;
});

builder.Services.Configure<BrevoSettings>(options =>
{
    options.Host = brevoSettings.Host;
    options.Port = brevoSettings.Port;
    options.Username = brevoSettings.Username;
    options.Password = brevoSettings.Password;
    options.FromEmail = brevoSettings.FromEmail;
    options.FromName = brevoSettings.FromName;
});

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register database context
builder.Services.AddSingleton<MongoDbContext>();

// Register HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Register Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStockTransactionRepository, StockTransactionRepository>();
builder.Services.AddScoped<ISaleRepository, SaleRepository>();

// Register Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IImageService, CloudinaryImageService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IReportService, ReportService>();

// Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

// Configure Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Startup Configuration Validation
if (string.IsNullOrWhiteSpace(mongoSettings.ConnectionString))
{
    app.Logger.LogError("MongoDB connection string not configured.");
    throw new InvalidOperationException("MongoDB connection string not configured.");
}

if (string.IsNullOrWhiteSpace(cloudinarySettings.CloudName) ||
    string.IsNullOrWhiteSpace(cloudinarySettings.ApiKey) ||
    string.IsNullOrWhiteSpace(cloudinarySettings.ApiSecret))
{
    app.Logger.LogError("Cloudinary configuration missing.");
    throw new InvalidOperationException("Cloudinary configuration missing.");
}

if (string.IsNullOrWhiteSpace(brevoSettings.Host) ||
    string.IsNullOrWhiteSpace(brevoSettings.Username) ||
    string.IsNullOrWhiteSpace(brevoSettings.Password) ||
    string.IsNullOrWhiteSpace(brevoSettings.FromEmail))
{
    app.Logger.LogWarning("Brevo SMTP configuration missing.");
}

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionMiddleware>();

// Explicitly register static physical file providers for wwwroot in both content root & execution directory
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
}

if (Directory.Exists(wwwrootPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(wwwrootPath),
        RequestPath = ""
    });
}

app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed database on startup
try
{
    await app.SeedDatabaseAsync();
}
catch (Exception ex)
{
    app.Logger.LogError("Database seeding failed: {Message}", ex.Message);
}

app.Run();
