namespace ClarissaBot.Api.Middleware;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Middleware that validates API key authentication for protected endpoints.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _apiKey;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private readonly bool _isDevelopment;
    private const string ApiKeyHeaderName = "X-API-Key";

    // Endpoints that don't require authentication
    private static readonly string[] PublicPaths = ["/api/health", "/health"];

    public ApiKeyMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _apiKey = configuration["ApiKey"] ?? configuration["API_KEY"];
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();

        if (string.IsNullOrEmpty(_apiKey))
        {
            if (_isDevelopment)
            {
                _logger.LogWarning("API Key is not configured. API endpoints are unprotected in development mode.");
            }
            else
            {
                // In production, missing API key is a critical configuration error
                throw new InvalidOperationException(
                    "API Key is required in production. " +
                    "Set 'ApiKey' in configuration or 'API_KEY' environment variable.");
            }
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Skip authentication for public endpoints
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        // Skip authentication only in development when no API key is configured
        if (string.IsNullOrEmpty(_apiKey) && _isDevelopment)
        {
            await _next(context);
            return;
        }

        // Check for API key in header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey))
        {
            _logger.LogWarning("API request rejected: Missing {Header} header from {IP}",
                ApiKeyHeaderName, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Unauthorized",
                status = 401,
                detail = $"Missing {ApiKeyHeaderName} header"
            });
            return;
        }

        // Validate API key using cryptographically secure constant-time comparison
        if (!SecureEquals(_apiKey!, providedApiKey.ToString()))
        {
            _logger.LogWarning("API request rejected: Invalid API key from {IP}",
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Unauthorized",
                status = 401,
                detail = "Invalid API key"
            });
            return;
        }

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        return PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Cryptographically secure constant-time comparison to prevent timing attacks.
    /// Uses CryptographicOperations.FixedTimeEquals which is immune to timing side-channels.
    /// </summary>
    private static bool SecureEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}

/// <summary>
/// Extension methods for API key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyMiddleware>();
    }
}

