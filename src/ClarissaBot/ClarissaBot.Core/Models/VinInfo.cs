namespace ClarissaBot.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents decoded VIN information from NHTSA.
/// </summary>
public record VinInfo
{
    [JsonPropertyName("Value")]
    public string? Value { get; init; }

    [JsonPropertyName("Variable")]
    public string? Variable { get; init; }

    [JsonPropertyName("VariableId")]
    public int? VariableId { get; init; }
}

/// <summary>
/// Response wrapper for VIN decode API.
/// </summary>
public record VinDecodeResponse
{
    [JsonPropertyName("Count")]
    public int Count { get; init; }

    [JsonPropertyName("Results")]
    public List<VinInfo>? Results { get; init; }
}

/// <summary>
/// Parsed vehicle information from a VIN.
/// </summary>
public record VehicleInfo
{
    public string? Vin { get; init; }
    public int? Year { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }
    public string? Trim { get; init; }
    public string? VehicleType { get; init; }
    public string? BodyClass { get; init; }
    public string? DriveType { get; init; }
    public string? FuelType { get; init; }
    public string? EngineSize { get; init; }
    public string? EngineCylinders { get; init; }
    public string? TransmissionStyle { get; init; }
    public string? PlantCountry { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorText { get; init; }
}

