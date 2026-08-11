# ============================================================
# SIMS – Multi-stage Production Dockerfile for Render (.NET 8)
# ============================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file first for layer caching
COPY ["InventoryManagementSystem.csproj", "./"]
RUN dotnet restore "InventoryManagementSystem.csproj"

# Copy remaining source code and publish
COPY . .
RUN dotnet publish "InventoryManagementSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published assets from build stage
COPY --from=build /app/publish .

# Expose Render PORT (default 8080 if not injected)
ENV PORT=8080
EXPOSE 8080

# Production ASP.NET Core Settings
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "InventoryManagementSystem.dll"]
