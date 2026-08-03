# SIMS – Smart Inventory Management System

An Enterprise ASP.NET Core (.NET 8) MVC Inventory Management System.

---

## 🛠️ Configuration & Secrets Management Guide

The application uses an enterprise-grade, multi-provider configuration system that works seamlessly across all IDEs, operating systems, and production environments without changing code.

### 🎛️ Configuration Resolution Order (Highest to Lowest Priority)

1. **Environment Variables / `.env` File** *(Overriding Priority)*
2. **User Secrets** *(Local Developer Machine)*
3. **`appsettings.Development.json`**
4. **`appsettings.json`** *(Fallback Defaults)*

---

### 📄 1. Configuring `.env` File

Copy `.env.example` to create `.env` in the root of the project:

```bash
cp .env.example .env
```

Set your credentials in `.env`:

```env
# MongoDB Configuration
MONGODB_CONNECTION_STRING=mongodb+srv://user:password@cluster.mongodb.net/?appName=Cluster0
MONGODB_DATABASE=SIMS_Db

# Cloudinary Storage Configuration
CLOUDINARY_CLOUD_NAME=your_cloud_name
CLOUDINARY_API_KEY=your_api_key
CLOUDINARY_API_SECRET=your_api_secret

# Brevo SMTP Configuration
BREVO_HOST=smtp-relay.brevo.com
BREVO_PORT=587
BREVO_USERNAME=your_brevo_smtp_login
BREVO_PASSWORD=your_brevo_smtp_password
BREVO_FROM_EMAIL=noreply@yourdomain.com
BREVO_FROM_NAME=SIMS System
```

> ⚠️ **SECURITY NOTICE**: `.env` contains sensitive secrets and is listed in `.gitignore`. **NEVER** commit `.env` files to git repository.

---

### 🔒 2. How to Use .NET User Secrets (Optional Alternative)

If you prefer using .NET User Secrets instead of `.env`:

```bash
dotnet user-secrets set "MongoDbSettings:ConnectionString" "mongodb+srv://..."
dotnet user-secrets set "MongoDbSettings:DatabaseName" "SIMS_Db"
dotnet user-secrets set "CloudinarySettings:CloudName" "your_cloud_name"
dotnet user-secrets set "CloudinarySettings:ApiKey" "your_api_key"
dotnet user-secrets set "CloudinarySettings:ApiSecret" "your_api_secret"
```

---

### 💻 3. IDE Setup Guides

#### 🔷 Microsoft Visual Studio 2022
1. Open `InventoryManagementSystem.sln`.
2. Ensure `.env` exists in the `InventoryManagementSystem` project folder (or workspace root).
3. Right-click project -> **Manage User Secrets** if you wish to use Visual Studio's User Secrets manager (`secrets.json`).
4. Press `F5` or `Ctrl+F5` to run. `DotNetEnv` automatically loads `.env` during `Program.cs` startup.

#### 🟦 Visual Studio Code (VS Code)
1. Open the project folder in VS Code.
2. Create `.env` in the root directory.
3. Install the C# Extension (`ms-dotnettools.csharp`).
4. Press `F5` or run `dotnet run` in the integrated terminal.

#### 🌌 Google Antigravity IDE
1. Ensure `.env` is created in the root workspace folder.
2. Run `dotnet run` in the Antigravity Terminal.
3. The app automatically loads variables and listens on `http://localhost:5094`.

#### 🔴 JetBrains Rider
1. Open the solution in Rider.
2. Ensure `.env` is present in the project directory.
3. Select `InventoryManagementSystem` run configuration and click **Run** (`Shift+F10`).

---

### 🚀 4. Production Deployment

In production environments (e.g. IIS, Azure App Service, Docker, Kubernetes, Linux Systemd):

Do not deploy `.env` or `appsettings.Development.json`. Set environment variables directly in your host system:

- **Environment Variables**:
  - `MONGODB_CONNECTION_STRING`
  - `MONGODB_DATABASE`
  - `CLOUDINARY_CLOUD_NAME`
  - `CLOUDINARY_API_KEY`
  - `CLOUDINARY_API_SECRET`
  - `BREVO_HOST`
  - `BREVO_PORT`
  - `BREVO_USERNAME`
  - `BREVO_PASSWORD`
  - `BREVO_FROM_EMAIL`
  - `BREVO_FROM_NAME`

---

### 🛑 Startup Validation Checks

During application startup, `Program.cs` validates required configuration parameters:

- **MongoDB Missing**: Logs error and throws `InvalidOperationException("MongoDB connection string not configured.")`.
- **Cloudinary Missing**: Logs error and throws `InvalidOperationException("Cloudinary configuration missing.")`.
- **Brevo Missing**: Logs warning `"Brevo SMTP configuration missing."`.
