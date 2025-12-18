namespace ClarissaBot.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a vehicle recall from NHTSA.
/// </summary>
public record RecallInfo
{
    [JsonPropertyName("NHTSACampaignNumber")]
    public string? CampaignNumber { get; init; }

    [JsonPropertyName("Manufacturer")]
    public string? Manufacturer { get; init; }

    [JsonPropertyName("Component")]
    public string? Component { get; init; }

    [JsonPropertyName("Summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("Consequence")]
    public string? Consequence { get; init; }

    [JsonPropertyName("Remedy")]
    public string? Remedy { get; init; }

    [JsonPropertyName("ReportReceivedDate")]
    public string? ReportReceivedDate { get; init; }

    [JsonPropertyName("ModelYear")]
    public string? ModelYear { get; init; }

    [JsonPropertyName("Make")]
    public string? Make { get; init; }

    [JsonPropertyName("Model")]
    public string? Model { get; init; }
}

/// <summary>
/// Response wrapper for recall API.
/// </summary>
public record RecallResponse
{
    [JsonPropertyName("Count")]
    public int Count { get; init; }

    [JsonPropertyName("results")]
    public List<RecallInfo>? Results { get; init; }
}

