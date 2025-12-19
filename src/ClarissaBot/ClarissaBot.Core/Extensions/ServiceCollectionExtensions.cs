namespace ClarissaBot.Core.Extensions;

using ClarissaBot.Core.Agent;
using ClarissaBot.Core.Services;
using ClarissaBot.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using OpenAI.Chat;
using Polly;

/// <summary>
/// Extension methods for configuring ClarissaBot services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ClarissaBot core services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddClarissaBotCore(this IServiceCollection services)
    {
        // Register memory cache for NHTSA response caching
        services.AddMemoryCache();

        // Register HttpClient for NHTSA API with resilience policies
        services.AddHttpClient<INhtsaService, NhtsaService>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddResilienceHandler("NhtsaResilience", builder =>
        {
            // Retry policy: exponential backoff for transient errors
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(response => (int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            });

            // Circuit breaker: open after 5 failures within 30 seconds
            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(response => (int)response.StatusCode >= 500)
            });

            // Timeout policy: per-request timeout
            builder.AddTimeout(TimeSpan.FromSeconds(10));
        });

        // Register NHTSA tools
        services.AddScoped<NhtsaTools>();

        return services;
    }

    /// <summary>
    /// Adds the Clarissa agent with Azure OpenAI integration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="chatClient">The Azure OpenAI chat client</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddClarissaAgent(this IServiceCollection services, ChatClient chatClient)
    {
        services.AddSingleton(chatClient);
        services.AddScoped<IClarissaAgent, ClarissaAgent>();
        return services;
    }
}

