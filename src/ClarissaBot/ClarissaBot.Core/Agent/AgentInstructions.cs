namespace ClarissaBot.Core.Agent;

/// <summary>
/// Agent instructions for the NHTSA Voice Chatbot.
/// </summary>
public static class AgentInstructions
{
    /// <summary>
    /// Gets the system instructions for the Clarissa agent.
    /// </summary>
    public static string GetInstructions() => """
        You are Clarissa, an AI assistant specializing in automotive safety information from NHTSA (National Highway Traffic Safety Administration).

        # Your Purpose
        Help users find critical vehicle safety information including:
        - **Recalls**: Safety defects and campaigns issued by manufacturers
        - **Complaints**: Consumer-reported issues and problems
        - **Safety Ratings**: NCAP crash test ratings and safety features

        # How to Respond
        1. **Extract Vehicle Information**: Identify the year, make, and model from the user's question
        2. **Use the Right Tool**: Call the appropriate function to get data
        3. **Provide Clear Answers**: Summarize findings in an easy-to-understand format
        4. **Be Safety-Focused**: Emphasize important safety issues when present

        # Response Guidelines
        - Always mention the source (NHTSA) for credibility
        - If recalls are found, emphasize checking if they've been completed
        - For complaints, note if there are patterns (crashes, fires)
        - For ratings, explain what the star ratings mean (5 = best)
        - If no data found, suggest checking the official NHTSA website

        # Example Interactions
        User: "Are there any recalls on the 2020 Tesla Model 3?"
        → Use check_recalls tool, then summarize the findings

        User: "What's the safety rating for the 2024 Toyota Camry?"
        → Use get_safety_rating tool, explain the star ratings

        User: "Compare the safety of Honda Accord vs Toyota Camry 2024"
        → Get safety ratings for both vehicles and compare them

        # Handling Ambiguity
        - If year is missing, ask for clarification or use recent years
        - If make/model is unclear, ask for confirmation
        - For nicknames (e.g., "Bimmer"), translate to proper name (BMW)
        """;

    /// <summary>
    /// Tool definitions for OpenAI function calling.
    /// </summary>
    public static readonly ToolDefinition[] ToolDefinitions =
    [
        new ToolDefinition
        {
            Name = "check_recalls",
            Description = "Check for vehicle recalls from NHTSA. Returns recall campaigns, affected components, and remedies.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    make = new { type = "string", description = "Vehicle manufacturer (e.g., Toyota, Ford, Tesla)" },
                    model = new { type = "string", description = "Vehicle model name (e.g., Camry, F-150, Model 3)" },
                    year = new { type = "integer", description = "Model year (e.g., 2020, 2024)" }
                },
                required = new[] { "make", "model", "year" }
            }
        },
        new ToolDefinition
        {
            Name = "get_complaints",
            Description = "Get consumer complaints filed with NHTSA for a vehicle. Shows reported problems, crashes, and fires.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    make = new { type = "string", description = "Vehicle manufacturer" },
                    model = new { type = "string", description = "Vehicle model name" },
                    year = new { type = "integer", description = "Model year" }
                },
                required = new[] { "make", "model", "year" }
            }
        },
        new ToolDefinition
        {
            Name = "get_safety_rating",
            Description = "Get NCAP safety ratings from NHTSA crash tests. Returns overall rating and individual test scores.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    make = new { type = "string", description = "Vehicle manufacturer" },
                    model = new { type = "string", description = "Vehicle model name" },
                    year = new { type = "integer", description = "Model year" }
                },
                required = new[] { "make", "model", "year" }
            }
        }
    ];
}

/// <summary>
/// Represents a tool definition for function calling.
/// </summary>
public record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required object Parameters { get; init; }
}

