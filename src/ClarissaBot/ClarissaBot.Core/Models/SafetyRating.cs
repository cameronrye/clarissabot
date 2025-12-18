namespace ClarissaBot.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents NCAP safety rating from NHTSA.
/// </summary>
public record SafetyRating
{
    [JsonPropertyName("VehicleId")]
    public int VehicleId { get; init; }

    [JsonPropertyName("VehicleDescription")]
    public string? VehicleDescription { get; init; }

    [JsonPropertyName("OverallRating")]
    public string? OverallRating { get; init; }

    [JsonPropertyName("OverallFrontCrashRating")]
    public string? OverallFrontCrashRating { get; init; }

    [JsonPropertyName("FrontCrashDriversideRating")]
    public string? FrontCrashDriversideRating { get; init; }

    [JsonPropertyName("FrontCrashPassengersideRating")]
    public string? FrontCrashPassengersideRating { get; init; }

    [JsonPropertyName("OverallSideCrashRating")]
    public string? OverallSideCrashRating { get; init; }

    [JsonPropertyName("SideCrashDriversideRating")]
    public string? SideCrashDriversideRating { get; init; }

    [JsonPropertyName("SideCrashPassengersideRating")]
    public string? SideCrashPassengersideRating { get; init; }

    [JsonPropertyName("RolloverRating")]
    public string? RolloverRating { get; init; }

    [JsonPropertyName("NHTSAForwardCollisionWarning")]
    public string? ForwardCollisionWarning { get; init; }

    [JsonPropertyName("NHTSALaneDepartureWarning")]
    public string? LaneDepartureWarning { get; init; }

    [JsonPropertyName("NHTSAElectronicStabilityControl")]
    public string? ElectronicStabilityControl { get; init; }

    [JsonPropertyName("ModelYear")]
    public int ModelYear { get; init; }

    [JsonPropertyName("Make")]
    public string? Make { get; init; }

    [JsonPropertyName("Model")]
    public string? Model { get; init; }
}

/// <summary>
/// Response wrapper for safety ratings lookup (vehicle list).
/// </summary>
public record SafetyRatingLookupResponse
{
    [JsonPropertyName("Count")]
    public int Count { get; init; }

    [JsonPropertyName("Results")]
    public List<SafetyRatingVehicle>? Results { get; init; }
}

/// <summary>
/// Vehicle info from safety ratings lookup.
/// </summary>
public record SafetyRatingVehicle
{
    [JsonPropertyName("VehicleId")]
    public int VehicleId { get; init; }

    [JsonPropertyName("VehicleDescription")]
    public string? VehicleDescription { get; init; }
}

/// <summary>
/// Response wrapper for detailed safety rating by VehicleId.
/// </summary>
public record SafetyRatingDetailResponse
{
    [JsonPropertyName("Count")]
    public int Count { get; init; }

    [JsonPropertyName("Results")]
    public List<SafetyRating>? Results { get; init; }
}

