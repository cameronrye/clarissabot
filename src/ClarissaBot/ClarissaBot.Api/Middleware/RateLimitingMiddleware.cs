namespace ClarissaBot.Api.Middleware;

using System.Collections.Concurrent;

/// <summary>
/// Simple sliding window rate limiting middleware.
/// Limits requests per IP address within a configurable time window.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();

    // ReSharper disable once NotAccessedField.Local - Timer must be kept alive to prevent GC
    private readonly Timer _cleanupTimer;

    // Endpoints exempt from rate limiting
    private static readonly string[] ExemptPaths = ["/api/health", "/health"];

    public RateLimitingMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 60);
        _window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:WindowSeconds", 60));

        // Cleanup old entries every minute
        _cleanupTimer = new Timer(CleanupOldEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Skip rate limiting for exempt paths
        if (ExemptPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var entry = _clients.GetOrAdd(clientId, _ => new RateLimitEntry());

        lock (entry)
        {
            // Remove expired timestamps
            var cutoff = DateTime.UtcNow - _window;
            while (entry.Timestamps.Count > 0 && entry.Timestamps.Peek() < cutoff)
            {
                entry.Timestamps.Dequeue();
            }

            // Check if limit exceeded
            if (entry.Timestamps.Count >= _permitLimit)
            {
                _logger.LogWarning("Rate limit exceeded for client {ClientId}: {Count} requests in {Window}",
                    clientId, entry.Timestamps.Count, _window);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/problem+json";
                
                // Calculate retry-after
                var oldestRequest = entry.Timestamps.Peek();
                var retryAfter = (int)Math.Ceiling((oldestRequest + _window - DateTime.UtcNow).TotalSeconds);
                retryAfter = Math.Max(1, retryAfter);
                
                context.Response.Headers.Append("Retry-After", retryAfter.ToString());
                context.Response.Headers.Append("X-RateLimit-Limit", _permitLimit.ToString());
                context.Response.Headers.Append("X-RateLimit-Remaining", "0");
                context.Response.Headers.Append("X-RateLimit-Reset", oldestRequest.Add(_window).ToString("O"));

                context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = 429,
                    detail = $"Rate limit exceeded. Try again in {retryAfter} seconds.",
                    retryAfter
                }).GetAwaiter().GetResult();
                
                return;
            }

            // Record this request
            entry.Timestamps.Enqueue(DateTime.UtcNow);
            
            // Add rate limit headers
            var remaining = _permitLimit - entry.Timestamps.Count;
            context.Response.Headers.Append("X-RateLimit-Limit", _permitLimit.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", remaining.ToString());
        }

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Use X-Forwarded-For if behind a proxy, otherwise use remote IP
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP (original client)
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private void CleanupOldEntries(object? state)
    {
        var cutoff = DateTime.UtcNow - _window - TimeSpan.FromMinutes(5);
        var keysToRemove = _clients
            .Where(kvp => kvp.Value.Timestamps.Count == 0 || 
                         (kvp.Value.Timestamps.Count > 0 && kvp.Value.Timestamps.Peek() < cutoff))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _clients.TryRemove(key, out _);
        }
    }

    private sealed class RateLimitEntry
    {
        public Queue<DateTime> Timestamps { get; } = new();
    }
}

/// <summary>
/// Extension methods for rate limiting middleware.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}

