namespace ClarissaBot.Core.Services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using ClarissaBot.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// NHTSA API client service.
/// </summary>
public class NhtsaService : INhtsaService
{
    private const string BaseUrl = "https://api.nhtsa.gov";
    
    private readonly HttpClient _httpClient;
    private readonly ILogger<NhtsaService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public NhtsaService(HttpClient httpClient, ILogger<NhtsaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<RecallResponse> GetRecallsAsync(string make, string model, int year, CancellationToken cancellationToken = default)
    {
        var encodedMake = HttpUtility.UrlEncode(make);
        var encodedModel = HttpUtility.UrlEncode(model);
        var url = $"{BaseUrl}/recalls/recallsByVehicle?make={encodedMake}&model={encodedModel}&modelYear={year}";

        _logger.LogInformation("Fetching recalls for {Year} {Make} {Model}", year, make, model);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<RecallResponse>(url, _jsonOptions, cancellationToken);
            return response ?? new RecallResponse { Count = 0, Results = [] };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recalls for {Year} {Make} {Model}", year, make, model);
            return new RecallResponse { Count = 0, Results = [] };
        }
    }

    /// <inheritdoc />
    public async Task<ComplaintResponse> GetComplaintsAsync(string make, string model, int year, CancellationToken cancellationToken = default)
    {
        var encodedMake = HttpUtility.UrlEncode(make);
        var encodedModel = HttpUtility.UrlEncode(model);
        var url = $"{BaseUrl}/complaints/complaintsByVehicle?make={encodedMake}&model={encodedModel}&modelYear={year}";

        _logger.LogInformation("Fetching complaints for {Year} {Make} {Model}", year, make, model);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<ComplaintResponse>(url, _jsonOptions, cancellationToken);
            return response ?? new ComplaintResponse { Count = 0, Results = [] };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching complaints for {Year} {Make} {Model}", year, make, model);
            return new ComplaintResponse { Count = 0, Results = [] };
        }
    }

    /// <inheritdoc />
    public async Task<SafetyRating?> GetSafetyRatingAsync(string make, string model, int year, CancellationToken cancellationToken = default)
    {
        var encodedMake = Uri.EscapeDataString(make);
        var encodedModel = Uri.EscapeDataString(model);
        var url = $"{BaseUrl}/SafetyRatings/modelyear/{year}/make/{encodedMake}/model/{encodedModel}";
        
        _logger.LogInformation("Fetching safety ratings for {Year} {Make} {Model}", year, make, model);
        
        try
        {
            // First, get the vehicle list to find the VehicleId
            var lookupResponse = await _httpClient.GetFromJsonAsync<SafetyRatingLookupResponse>(url, _jsonOptions, cancellationToken);
            
            if (lookupResponse?.Results is not { Count: > 0 })
            {
                _logger.LogWarning("No safety rating found for {Year} {Make} {Model}", year, make, model);
                return null;
            }

            // Get the first vehicle's detailed rating
            var vehicleId = lookupResponse.Results[0].VehicleId;
            var detailUrl = $"{BaseUrl}/SafetyRatings/VehicleId/{vehicleId}";
            
            var detailResponse = await _httpClient.GetFromJsonAsync<SafetyRatingDetailResponse>(detailUrl, _jsonOptions, cancellationToken);
            
            return detailResponse?.Results?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching safety rating for {Year} {Make} {Model}", year, make, model);
            return null;
        }
    }
}

