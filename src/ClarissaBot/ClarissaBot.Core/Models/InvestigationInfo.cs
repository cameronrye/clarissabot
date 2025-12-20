namespace ClarissaBot.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents an NHTSA defect investigation.
/// </summary>
public record InvestigationInfo
{
    [JsonPropertyName("CAMPNO")]
    public string? CampaignNumber { get; init; }

    [JsonPropertyName("COMPNAME")]
    public string? ComponentName { get; init; }

    [JsonPropertyName("MFR_NAME")]
    public string? ManufacturerName { get; init; }

    [JsonPropertyName("SUBJECT")]
    public string? Subject { get; init; }

    [JsonPropertyName("SUMMARY")]
    public string? Summary { get; init; }

    [JsonPropertyName("AESSION")]
    public string? AccessionNumber { get; init; }

    [JsonPropertyName("DESSION")]
    public string? DecisionNumber { get; init; }

    [JsonPropertyName("CESSION")]
    public string? CessionNumber { get; init; }

    [JsonPropertyName("OESSION")]
    public string? OpenSession { get; init; }

    [JsonPropertyName("CLESSION")]
    public string? CloseSession { get; init; }

    [JsonPropertyName("RECALLREASON")]
    public string? RecallReason { get; init; }
}

/// <summary>
/// Response wrapper for investigations API.
/// </summary>
public record InvestigationResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("results")]
    public List<InvestigationInfo>? Results { get; init; }
}

