namespace ClarissaBot.Core.Services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using ClarissaBot.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// NHTSA API client service with response caching.
/// </summary>
public class NhtsaService : INhtsaService
{
    private const string BaseUrl = "https://api.nhtsa.gov";

    // Cache TTLs - recalls and safety data change infrequently
    private static readonly TimeSpan RecallCacheTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan ComplaintCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan SafetyRatingCacheTtl = TimeSpan.FromHours(24);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NhtsaService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public NhtsaService(HttpClient httpClient, IMemoryCache cache, ILogger<NhtsaService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private static string GetRecallCacheKey(string make, string model, int year)
        => $"nhtsa:recalls:{make.ToLowerInvariant()}:{model.ToLowerInvariant()}:{year}";

    private static string GetComplaintCacheKey(string make, string model, int year)
        => $"nhtsa:complaints:{make.ToLowerInvariant()}:{model.ToLowerInvariant()}:{year}";

    private static string GetSafetyRatingCacheKey(string make, string model, int year)
        => $"nhtsa:safety:{make.ToLowerInvariant()}:{model.ToLowerInvariant()}:{year}";

    /// <inheritdoc />
    public async Task<RecallResponse> GetRecallsAsync(string make, string model, int year, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetRecallCacheKey(make, model, year);

        if (_cache.TryGetValue(cacheKey, out RecallResponse? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit for recalls: {Year} {Make} {Model}", year, make, model);
            return cached;
        }

        var encodedMake = HttpUtility.UrlEncode(make);
        var encodedModel = HttpUtility.UrlEncode(model);
        var url = $"{BaseUrl}/recalls/recallsByVehicle?make={encodedMake}&model={encodedModel}&modelYear={year}";

        _logger.LogInformation("Fetching recalls for {Year} {Make} {Model}", year, make, model);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<RecallResponse>(url, _jsonOptions, cancellationToken);
            var result = response ?? new RecallResponse { Count = 0, Results = [] };

            _cache.Set(cacheKey, result, RecallCacheTtl);
            return result;
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
        var cacheKey = GetComplaintCacheKey(make, model, year);

        if (_cache.TryGetValue(cacheKey, out ComplaintResponse? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit for complaints: {Year} {Make} {Model}", year, make, model);
            return cached;
        }

        var encodedMake = HttpUtility.UrlEncode(make);
        var encodedModel = HttpUtility.UrlEncode(model);
        var url = $"{BaseUrl}/complaints/complaintsByVehicle?make={encodedMake}&model={encodedModel}&modelYear={year}";

        _logger.LogInformation("Fetching complaints for {Year} {Make} {Model}", year, make, model);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<ComplaintResponse>(url, _jsonOptions, cancellationToken);
            var result = response ?? new ComplaintResponse { Count = 0, Results = [] };

            _cache.Set(cacheKey, result, ComplaintCacheTtl);
            return result;
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
        var cacheKey = GetSafetyRatingCacheKey(make, model, year);

        if (_cache.TryGetValue(cacheKey, out SafetyRating? cached))
        {
            _logger.LogDebug("Cache hit for safety rating: {Year} {Make} {Model}", year, make, model);
            return cached;
        }

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
                // Cache null result to avoid repeated lookups for unknown vehicles
                _cache.Set(cacheKey, (SafetyRating?)null, SafetyRatingCacheTtl);
                return null;
            }

            // Get the first vehicle's detailed rating
            var vehicleId = lookupResponse.Results[0].VehicleId;
            var detailUrl = $"{BaseUrl}/SafetyRatings/VehicleId/{vehicleId}";

            var detailResponse = await _httpClient.GetFromJsonAsync<SafetyRatingDetailResponse>(detailUrl, _jsonOptions, cancellationToken);
            var result = detailResponse?.Results?.FirstOrDefault();

            _cache.Set(cacheKey, result, SafetyRatingCacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching safety rating for {Year} {Make} {Model}", year, make, model);
            return null;
        }
    }
}

