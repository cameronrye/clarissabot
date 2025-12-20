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
    private const string VpicBaseUrl = "https://vpic.nhtsa.dot.gov/api";

    // Cache TTLs - recalls and safety data change infrequently
    private static readonly TimeSpan RecallCacheTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan ComplaintCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan SafetyRatingCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan VinCacheTtl = TimeSpan.FromDays(30); // VINs don't change
    private static readonly TimeSpan InvestigationCacheTtl = TimeSpan.FromHours(6);

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

    private static string GetVinCacheKey(string vin)
        => $"nhtsa:vin:{vin.ToUpperInvariant()}";

    private static string GetInvestigationCacheKey(string make, string model, int year)
        => $"nhtsa:investigations:{make.ToLowerInvariant()}:{model.ToLowerInvariant()}:{year}";

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

    /// <inheritdoc />
    public async Task<VehicleInfo?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
        {
            return new VehicleInfo
            {
                Vin = vin,
                ErrorCode = "INVALID_VIN",
                ErrorText = "VIN must be exactly 17 characters"
            };
        }

        var cacheKey = GetVinCacheKey(vin);

        if (_cache.TryGetValue(cacheKey, out VehicleInfo? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit for VIN: {Vin}", vin);
            return cached;
        }

        var url = $"{VpicBaseUrl}/vehicles/DecodeVinValues/{Uri.EscapeDataString(vin)}?format=json";

        _logger.LogInformation("Decoding VIN: {Vin}", vin);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<VinDecodeResponse>(url, _jsonOptions, cancellationToken);

            if (response?.Results is not { Count: > 0 })
            {
                return new VehicleInfo { Vin = vin, ErrorCode = "NOT_FOUND", ErrorText = "Could not decode VIN" };
            }

            var results = response.Results;
            var result = new VehicleInfo
            {
                Vin = vin,
                Year = ParseInt(GetValue(results, "Model Year")),
                Make = GetValue(results, "Make"),
                Model = GetValue(results, "Model"),
                Trim = GetValue(results, "Trim"),
                VehicleType = GetValue(results, "Vehicle Type"),
                BodyClass = GetValue(results, "Body Class"),
                DriveType = GetValue(results, "Drive Type"),
                FuelType = GetValue(results, "Fuel Type - Primary"),
                EngineSize = GetValue(results, "Displacement (L)"),
                EngineCylinders = GetValue(results, "Engine Number of Cylinders"),
                TransmissionStyle = GetValue(results, "Transmission Style"),
                PlantCountry = GetValue(results, "Plant Country"),
                ErrorCode = GetValue(results, "Error Code"),
                ErrorText = GetValue(results, "Error Text")
            };

            _cache.Set(cacheKey, result, VinCacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding VIN: {Vin}", vin);
            return new VehicleInfo { Vin = vin, ErrorCode = "API_ERROR", ErrorText = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<InvestigationResponse> GetInvestigationsAsync(string make, string model, int year, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetInvestigationCacheKey(make, model, year);

        if (_cache.TryGetValue(cacheKey, out InvestigationResponse? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit for investigations: {Year} {Make} {Model}", year, make, model);
            return cached;
        }

        var encodedMake = HttpUtility.UrlEncode(make);
        var encodedModel = HttpUtility.UrlEncode(model);
        var url = $"{BaseUrl}/products/vehicle/makes/{encodedMake}/models/{encodedModel}/modelYears/{year}/investigations?format=json";

        _logger.LogInformation("Fetching investigations for {Year} {Make} {Model}", year, make, model);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<InvestigationResponse>(url, _jsonOptions, cancellationToken);
            var result = response ?? new InvestigationResponse { Count = 0, Results = [] };

            _cache.Set(cacheKey, result, InvestigationCacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching investigations for {Year} {Make} {Model}", year, make, model);
            return new InvestigationResponse { Count = 0, Results = [] };
        }
    }

    private static string? GetValue(List<VinInfo> results, string variable)
    {
        var value = results.FirstOrDefault(r => r.Variable == variable)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var result) ? result : null;
    }
}

