using System.Text.Json.Serialization;

namespace ClarissaBot.Core.Models;

/// <summary>
/// Represents a streaming event sent from the agent to the client.
/// </summary>
[JsonDerivedType(typeof(ContentChunkEvent), typeDiscriminator: "chunk")]
[JsonDerivedType(typeof(ToolCallEvent), typeDiscriminator: "toolCall")]
[JsonDerivedType(typeof(ToolResultEvent), typeDiscriminator: "toolResult")]
[JsonDerivedType(typeof(VehicleContextEvent), typeDiscriminator: "vehicleContext")]
public abstract record StreamingEvent
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// A chunk of text content from the assistant's response.
/// </summary>
public record ContentChunkEvent(string Content) : StreamingEvent
{
    [JsonPropertyName("type")]
    public override string Type => "chunk";

    [JsonPropertyName("content")]
    public string Content { get; init; } = Content;
}

/// <summary>
/// Notification that a tool is being called.
/// </summary>
public record ToolCallEvent(string ToolName, string? VehicleInfo = null) : StreamingEvent
{
    [JsonPropertyName("type")]
    public override string Type => "toolCall";

    [JsonPropertyName("toolName")]
    public string ToolName { get; init; } = ToolName;

    [JsonPropertyName("vehicleInfo")]
    public string? VehicleInfo { get; init; } = VehicleInfo;

    /// <summary>
    /// Gets a user-friendly description of the tool being called.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description => ToolName switch
    {
        "check_recalls" => "Checking recalls",
        "get_complaints" => "Getting complaints",
        "get_safety_rating" => "Getting safety ratings",
        "decode_vin" => "Decoding VIN",
        "check_investigations" => "Checking investigations",
        _ => "Looking up data"
    };
}

/// <summary>
/// Notification that a tool call has completed.
/// </summary>
public record ToolResultEvent(string ToolName, bool Success) : StreamingEvent
{
    [JsonPropertyName("type")]
    public override string Type => "toolResult";

    [JsonPropertyName("toolName")]
    public string ToolName { get; init; } = ToolName;

    [JsonPropertyName("success")]
    public bool Success { get; init; } = Success;
}

/// <summary>
/// Notification of the current vehicle context.
/// </summary>
public record VehicleContextEvent(int Year, string Make, string Model) : StreamingEvent
{
    [JsonPropertyName("type")]
    public override string Type => "vehicleContext";

    [JsonPropertyName("year")]
    public int Year { get; init; } = Year;

    [JsonPropertyName("make")]
    public string Make { get; init; } = Make;

    [JsonPropertyName("model")]
    public string Model { get; init; } = Model;

    [JsonPropertyName("display")]
    public string Display => $"{Year} {Make} {Model}";
}

