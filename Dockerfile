# ============================================================
# SIMS – Production Dockerfile for Render (Repository Root)
# ============================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file from subfolder for layer caching
COPY ["InventoryManagementSystem/InventoryManagementSystem.csproj", "InventoryManagementSystem/"]
RUN dotnet restore "InventoryManagementSystem/InventoryManagementSystem.csproj"

# Copy all source files
COPY . .
WORKDIR "/src/InventoryManagementSystem"
RUN dotnet publish "InventoryManagementSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose Render PORT (default 8080 if not injected)
ENV PORT=8080
EXPOSE 8080

# Production ASP.NET Core Settings
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "InventoryManagementSystem.dll"]
