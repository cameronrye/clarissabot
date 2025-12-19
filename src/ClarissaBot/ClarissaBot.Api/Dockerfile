# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ClarissaBot.Core/ClarissaBot.Core.csproj ClarissaBot.Core/
COPY ClarissaBot.Api/ClarissaBot.Api.csproj ClarissaBot.Api/

# Restore dependencies
RUN dotnet restore ClarissaBot.Api/ClarissaBot.Api.csproj

# Copy source code
COPY ClarissaBot.Core/ ClarissaBot.Core/
COPY ClarissaBot.Api/ ClarissaBot.Api/

# Build and publish (with restore to ensure all packages are available)
WORKDIR /src/ClarissaBot.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published app (the .NET 8+ images already have a non-root 'app' user)
COPY --from=build /app/publish .

# Use the built-in non-root user from .NET 8+ images
USER app

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Health check (using wget as it's available in aspnet images, unlike curl)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "ClarissaBot.Api.dll"]

