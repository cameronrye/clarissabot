namespace ClarissaBot.Core.Extensions;

using ClarissaBot.Core.Agent;
using ClarissaBot.Core.Services;
using ClarissaBot.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

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
        // Register HttpClient for NHTSA API
        services.AddHttpClient<INhtsaService, NhtsaService>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
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

