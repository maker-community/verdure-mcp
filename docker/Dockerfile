# Verdure MCP Server - Single Image Dockerfile
# This Dockerfile builds a single image containing both the API and Blazor WebAssembly frontend
# Optimized for minimal image size using Alpine Linux

# Stage 1: Base runtime image (Alpine for minimal size)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Install required tools for Alpine Linux:
# - curl: for health checks
# - brotli: for Brotli compression (.br files)
# - gzip: for Gzip compression (.gz files)
# - icu-libs: for globalization support (.NET requires ICU)
# - tzdata: for timezone support
RUN apk add --no-cache curl brotli gzip icu-libs tzdata

# Stage 2: Build image
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution file and build configuration
COPY ["Verdure.Mcp.sln", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]

# Copy all project files for dependency restoration
COPY ["src/Verdure.Mcp.Server/Verdure.Mcp.Server.csproj", "src/Verdure.Mcp.Server/"]
COPY ["src/Verdure.Mcp.Web/Verdure.Mcp.Web.csproj", "src/Verdure.Mcp.Web/"]
COPY ["src/Verdure.Mcp.Domain/Verdure.Mcp.Domain.csproj", "src/Verdure.Mcp.Domain/"]
COPY ["src/Verdure.Mcp.Infrastructure/Verdure.Mcp.Infrastructure.csproj", "src/Verdure.Mcp.Infrastructure/"]
COPY ["src/Verdure.Mcp.Shared/Verdure.Mcp.Shared.csproj", "src/Verdure.Mcp.Shared/"]

# Restore dependencies
# Note: Server project references Web project, so restoring Server will restore Web as well
RUN dotnet restore "src/Verdure.Mcp.Server/Verdure.Mcp.Server.csproj"

# Copy all source code
COPY . .

# Build the solution
# Building Server project will automatically build Web project as well (due to project reference)
WORKDIR "/src/src/Verdure.Mcp.Server"
RUN dotnet build "Verdure.Mcp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

# Publish Server project (includes Web static files in wwwroot)
RUN dotnet publish "Verdure.Mcp.Server.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 4: Final runtime image
FROM base AS final
WORKDIR /app

# Copy published files from publish stage
COPY --from=publish /app/publish .

# Copy entrypoint script and fix line endings
COPY docker/entrypoint.sh /entrypoint.sh
# Convert CRLF to LF (Windows to Unix line endings) and make executable
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Optional: Enable globalization support
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1

# Entry point - use custom script to handle mounted config files
ENTRYPOINT ["/entrypoint.sh"]
