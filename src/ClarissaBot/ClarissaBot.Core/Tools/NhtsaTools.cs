namespace ClarissaBot.Core.Tools;

using System.Text.Json;
using ClarissaBot.Core.Services;

/// <summary>
/// NHTSA tool functions for agent function calling.
/// </summary>
public class NhtsaTools
{
    private readonly INhtsaService _nhtsaService;

    public NhtsaTools(INhtsaService nhtsaService)
    {
        _nhtsaService = nhtsaService;
    }

    /// <summary>
    /// Check recalls for a specific vehicle.
    /// </summary>
    public async Task<string> CheckRecallsAsync(string make, string model, int year)
    {
        var recalls = await _nhtsaService.GetRecallsAsync(make, model, year);
        
        if (recalls.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                found = false,
                message = $"No recalls found for {year} {make} {model}."
            });
        }

        var recallSummaries = recalls.Results?.Take(5).Select(r => new
        {
            campaignNumber = r.CampaignNumber,
            component = r.Component,
            summary = r.Summary?.Length > 200 ? r.Summary[..200] + "..." : r.Summary
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            found = true,
            count = recalls.Count,
            recalls = recallSummaries,
            nhtsaLink = $"https://www.nhtsa.gov/vehicle/{year}/{make}/{model}#recalls"
        });
    }

    /// <summary>
    /// Get consumer complaints for a specific vehicle.
    /// </summary>
    public async Task<string> GetComplaintsAsync(string make, string model, int year)
    {
        var complaints = await _nhtsaService.GetComplaintsAsync(make, model, year);

        if (complaints.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                found = false,
                message = $"No complaints found for {year} {make} {model}."
            });
        }

        var topComplaints = complaints.Results?.Take(5).Select(c => new
        {
            component = c.Components,
            summary = c.Summary?.Length > 200 ? c.Summary[..200] + "..." : c.Summary,
            crash = c.Crash,
            fire = c.Fire
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            found = true,
            count = complaints.Count,
            complaints = topComplaints,
            nhtsaLink = $"https://www.nhtsa.gov/vehicle/{year}/{make}/{model}#complaints"
        });
    }

    /// <summary>
    /// Get NCAP safety rating for a specific vehicle.
    /// </summary>
    public async Task<string> GetSafetyRatingAsync(string make, string model, int year)
    {
        var rating = await _nhtsaService.GetSafetyRatingAsync(make, model, year);
        
        if (rating == null)
        {
            return JsonSerializer.Serialize(new
            {
                found = false,
                message = $"No safety rating found for {year} {make} {model}."
            });
        }

        return JsonSerializer.Serialize(new
        {
            found = true,
            vehicleDescription = rating.VehicleDescription,
            overallRating = rating.OverallRating,
            frontCrashRating = rating.OverallFrontCrashRating,
            sideCrashRating = rating.OverallSideCrashRating,
            rolloverRating = rating.RolloverRating,
            forwardCollisionWarning = rating.ForwardCollisionWarning,
            laneDepartureWarning = rating.LaneDepartureWarning,
            nhtsaLink = $"https://www.nhtsa.gov/vehicle/{year}/{make}/{model}"
        });
    }

    /// <summary>
    /// Decode a VIN to get vehicle information.
    /// </summary>
    public async Task<string> DecodeVinAsync(string vin)
    {
        var vehicle = await _nhtsaService.DecodeVinAsync(vin);

        if (vehicle == null || vehicle.ErrorCode == "INVALID_VIN" || vehicle.ErrorCode == "NOT_FOUND")
        {
            return JsonSerializer.Serialize(new
            {
                found = false,
                message = vehicle?.ErrorText ?? "Could not decode VIN",
                vin
            });
        }

        return JsonSerializer.Serialize(new
        {
            found = true,
            vin = vehicle.Vin,
            year = vehicle.Year,
            make = vehicle.Make,
            model = vehicle.Model,
            trim = vehicle.Trim,
            vehicleType = vehicle.VehicleType,
            bodyClass = vehicle.BodyClass,
            driveType = vehicle.DriveType,
            fuelType = vehicle.FuelType,
            engineSize = vehicle.EngineSize,
            engineCylinders = vehicle.EngineCylinders,
            transmissionStyle = vehicle.TransmissionStyle,
            plantCountry = vehicle.PlantCountry
        });
    }

    /// <summary>
    /// Check for active NHTSA investigations on a vehicle.
    /// </summary>
    public async Task<string> CheckInvestigationsAsync(string make, string model, int year)
    {
        var investigations = await _nhtsaService.GetInvestigationsAsync(make, model, year);

        if (investigations.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                found = false,
                message = $"No active investigations found for {year} {make} {model}."
            });
        }

        var investigationSummaries = investigations.Results?.Take(5).Select(i => new
        {
            campaignNumber = i.CampaignNumber,
            component = i.ComponentName,
            subject = i.Subject,
            summary = i.Summary?.Length > 200 ? i.Summary[..200] + "..." : i.Summary
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            found = true,
            count = investigations.Count,
            investigations = investigationSummaries,
            nhtsaLink = $"https://www.nhtsa.gov/vehicle/{year}/{make}/{model}#investigations"
        });
    }
}

