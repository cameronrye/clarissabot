# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser

# Copy published app
COPY --from=build /app/publish .

# Set ownership to non-root user
RUN chown -R appuser:appuser /app
USER appuser

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "ClarissaBot.Api.dll"]

