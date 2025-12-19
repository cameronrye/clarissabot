using Azure.AI.OpenAI;
using Azure.Identity;
using ClarissaBot.Core.Agent;
using ClarissaBot.Core.Extensions;
using ClarissaBot.Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Build service provider
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise in chat mode
});

services.AddClarissaBotCore();

// Check for Azure AI configuration
var projectEndpoint = configuration["AzureAI:ProjectEndpoint"];
var modelDeployment = configuration["AzureAI:ModelDeploymentName"] ?? "gpt-4o";
var useAiAgent = !string.IsNullOrEmpty(projectEndpoint);

if (useAiAgent)
{
    // Configure Azure OpenAI client with DefaultAzureCredential
    var openAIClient = new AzureOpenAIClient(
        new Uri(projectEndpoint!),
        new DefaultAzureCredential());

    var chatClient = openAIClient.GetChatClient(modelDeployment);
    services.AddClarissaAgent(chatClient);
}

var serviceProvider = services.BuildServiceProvider();
var tools = serviceProvider.GetRequiredService<NhtsaTools>();
IClarissaAgent? agent = useAiAgent ? serviceProvider.GetRequiredService<IClarissaAgent>() : null;

// Print welcome banner
Console.ForegroundColor = ConsoleColor.Cyan;
var modeText = useAiAgent ? "AI-Powered Chat Mode" : "Command Mode";
Console.WriteLine($"""

    ╔═══════════════════════════════════════════════════════════╗
    ║                                                           ║
    ║   🚗  CLARISSA - NHTSA Vehicle Safety Assistant  🚗      ║
    ║   Mode: {modeText,-48} ║
    ║                                                           ║
""");

if (useAiAgent)
{
    Console.WriteLine("""
    ║   Just ask me anything about vehicle safety!              ║
    ║   Example: "Are there any recalls on the 2020 Tesla 3?"   ║
""");
}
else
{
    Console.WriteLine("""
    ║   Commands:                                               ║
    ║   • recalls <year> <make> <model>  - Check recalls        ║
    ║   • complaints <year> <make> <model> - Get complaints     ║
    ║   • safety <year> <make> <model>   - Safety ratings       ║
""");
}

Console.WriteLine("""
    ║   • clear - Clear conversation history                    ║
    ║   • exit  - Quit the application                          ║
    ║                                                           ║
    ╚═══════════════════════════════════════════════════════════╝

""");
Console.ResetColor();

// Main interaction loop
while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("clarissa> ");
    Console.ResetColor();

    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input))
        continue;

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye! Drive safely! 🚗");
        break;
    }

    if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
    {
        agent?.ClearConversation();
        Console.WriteLine("Conversation cleared.");
        continue;
    }

    try
    {
        if (agent != null)
        {
            // AI-powered chat mode with streaming
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\n🚗 Clarissa: ");

            var hasContent = false;
            await foreach (var token in agent.ChatStreamAsync(input))
            {
                hasContent = true;
                Console.Write(token);
            }

            if (!hasContent)
            {
                Console.Write("I'm sorry, I couldn't generate a response. Please try again.");
            }

            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();
        }
        else
        {
            // Command mode
            await ProcessCommandAsync(input, tools);
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
    }
}

static async Task ProcessCommandAsync(string input, NhtsaTools tools)
{
    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 4)
    {
        ShowHelp();
        return;
    }

    var command = parts[0].ToLowerInvariant();
    if (!int.TryParse(parts[1], out var year))
    {
        Console.WriteLine("Invalid year. Please enter a valid year (e.g., 2024).");
        return;
    }

    var make = parts[2];
    var model = string.Join(" ", parts[3..]);

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"\n🔍 Looking up {year} {make} {model}...\n");
    Console.ResetColor();

    string result = command switch
    {
        "recalls" => await tools.CheckRecallsAsync(make, model, year),
        "complaints" => await tools.GetComplaintsAsync(make, model, year),
        "safety" or "rating" => await tools.GetSafetyRatingAsync(make, model, year),
        _ => throw new ArgumentException($"Unknown command: {command}")
    };

    // Pretty print the JSON result
    using var doc = System.Text.Json.JsonDocument.Parse(result);
    var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
    var formatted = System.Text.Json.JsonSerializer.Serialize(doc, options);

    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine(formatted);
    Console.ResetColor();
    Console.WriteLine();
}

static void ShowHelp()
{
    Console.WriteLine("""
        Usage: <command> <year> <make> <model>

        Commands:
          recalls     - Check for vehicle recalls
          complaints  - Get consumer complaints
          safety      - Get NCAP safety ratings

        Examples:
          recalls 2020 Tesla Model 3
          complaints 2024 Toyota Camry
          safety 2024 Honda Accord
        """);
}
