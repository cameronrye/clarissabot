namespace ClarissaBot.Core.Services;

using ClarissaBot.Core.Models;

/// <summary>
/// Interface for NHTSA API operations.
/// </summary>
public interface INhtsaService
{
    /// <summary>
    /// Get recalls for a specific vehicle.
    /// </summary>
    /// <param name="make">Vehicle make (e.g., "Toyota")</param>
    /// <param name="model">Vehicle model (e.g., "Camry")</param>
    /// <param name="year">Model year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recall response with count and results</returns>
    Task<RecallResponse> GetRecallsAsync(string make, string model, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get complaints for a specific vehicle.
    /// </summary>
    /// <param name="make">Vehicle make</param>
    /// <param name="model">Vehicle model</param>
    /// <param name="year">Model year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complaint response with count and results</returns>
    Task<ComplaintResponse> GetComplaintsAsync(string make, string model, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get safety ratings for a specific vehicle.
    /// </summary>
    /// <param name="make">Vehicle make</param>
    /// <param name="model">Vehicle model</param>
    /// <param name="year">Model year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Safety rating or null if not found</returns>
    Task<SafetyRating?> GetSafetyRatingAsync(string make, string model, int year, CancellationToken cancellationToken = default);
}

