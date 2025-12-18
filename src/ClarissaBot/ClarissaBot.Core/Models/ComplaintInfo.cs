namespace ClarissaBot.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a consumer complaint from NHTSA.
/// </summary>
public record ComplaintInfo
{
    /// <summary>
    /// NHTSA's internal reference number. The API returns this as a number.
    /// </summary>
    [JsonPropertyName("odiNumber")]
    public long? OdiNumber { get; init; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; init; }

    [JsonPropertyName("components")]
    public string? Components { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("dateOfIncident")]
    public string? DateOfIncident { get; init; }

    [JsonPropertyName("dateComplaintFiled")]
    public string? DateComplaintFiled { get; init; }

    [JsonPropertyName("crash")]
    public bool? Crash { get; init; }

    [JsonPropertyName("fire")]
    public bool? Fire { get; init; }

    [JsonPropertyName("numberOfInjuries")]
    public int? NumberOfInjuries { get; init; }

    [JsonPropertyName("numberOfDeaths")]
    public int? NumberOfDeaths { get; init; }

    [JsonPropertyName("modelYear")]
    public string? ModelYear { get; init; }

    [JsonPropertyName("make")]
    public string? Make { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

/// <summary>
/// Response wrapper for complaints API.
/// </summary>
public record ComplaintResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("results")]
    public List<ComplaintInfo>? Results { get; init; }
}

