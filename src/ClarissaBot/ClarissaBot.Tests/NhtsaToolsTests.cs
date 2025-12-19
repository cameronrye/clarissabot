namespace ClarissaBot.Tests;

using System.Text.Json;
using ClarissaBot.Core.Models;
using ClarissaBot.Core.Services;
using ClarissaBot.Core.Tools;
using Moq;

public class NhtsaToolsTests
{
    private readonly Mock<INhtsaService> _mockNhtsaService;
    private readonly NhtsaTools _tools;

    public NhtsaToolsTests()
    {
        _mockNhtsaService = new Mock<INhtsaService>();
        _tools = new NhtsaTools(_mockNhtsaService.Object);
    }

    #region CheckRecallsAsync Tests

    [Fact]
    public async Task CheckRecallsAsync_ReturnsNotFound_WhenNoRecalls()
    {
        // Arrange
        _mockNhtsaService.Setup(s => s.GetRecallsAsync("Toyota", "Camry", 2023, default))
            .ReturnsAsync(new RecallResponse { Count = 0, Results = [] });

        // Act
        var result = await _tools.CheckRecallsAsync("Toyota", "Camry", 2023);
        var json = JsonDocument.Parse(result);

        // Assert
        Assert.False(json.RootElement.GetProperty("found").GetBoolean());
        Assert.Contains("No recalls found", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task CheckRecallsAsync_ReturnsRecalls_WhenFound()
    {
        // Arrange
        var recalls = new RecallResponse
        {
            Count = 2,
            Results =
            [
                new RecallInfo { CampaignNumber = "20V123000", Component = "AIR BAGS", Summary = "Airbag may not deploy" },
                new RecallInfo { CampaignNumber = "20V456000", Component = "BRAKES", Summary = "Brake fluid leak" }
            ]
        };
        _mockNhtsaService.Setup(s => s.GetRecallsAsync("Tesla", "Model 3", 2020, default))
            .ReturnsAsync(recalls);

        // Act
        var result = await _tools.CheckRecallsAsync("Tesla", "Model 3", 2020);
        var json = JsonDocument.Parse(result);

        // Assert
        Assert.True(json.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("recalls").GetArrayLength());
    }

    [Fact]
    public async Task CheckRecallsAsync_TruncatesLongSummaries()
    {
        // Arrange
        var longSummary = new string('A', 300);
        var recalls = new RecallResponse
        {
            Count = 1,
            Results = [new RecallInfo { CampaignNumber = "20V123000", Component = "ENGINE", Summary = longSummary }]
        };
        _mockNhtsaService.Setup(s => s.GetRecallsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), default))
            .ReturnsAsync(recalls);

        // Act
        var result = await _tools.CheckRecallsAsync("Honda", "Accord", 2022);
        var json = JsonDocument.Parse(result);

        // Assert
        var recallSummary = json.RootElement.GetProperty("recalls")[0].GetProperty("summary").GetString();
        Assert.NotNull(recallSummary);
        Assert.True(recallSummary.Length <= 203); // 200 + "..."
        Assert.EndsWith("...", recallSummary);
    }

    [Fact]
    public async Task CheckRecallsAsync_LimitsFiveRecalls()
    {
        // Arrange
        var recalls = new RecallResponse
        {
            Count = 10,
            Results = Enumerable.Range(1, 10)
                .Select(i => new RecallInfo { CampaignNumber = $"20V{i:000}000", Component = "TEST", Summary = $"Recall {i}" })
                .ToList()
        };
        _mockNhtsaService.Setup(s => s.GetRecallsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), default))
            .ReturnsAsync(recalls);

        // Act
        var result = await _tools.CheckRecallsAsync("Ford", "F-150", 2021);
        var json = JsonDocument.Parse(result);

        // Assert
        Assert.Equal(10, json.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(5, json.RootElement.GetProperty("recalls").GetArrayLength());
    }

    #endregion

    #region GetComplaintsAsync Tests

    [Fact]
    public async Task GetComplaintsAsync_ReturnsNotFound_WhenNoComplaints()
    {
        // Arrange
        _mockNhtsaService.Setup(s => s.GetComplaintsAsync("Honda", "Civic", 2024, default))
            .ReturnsAsync(new ComplaintResponse { Count = 0, Results = [] });

        // Act
        var result = await _tools.GetComplaintsAsync("Honda", "Civic", 2024);
        var json = JsonDocument.Parse(result);

        // Assert
        Assert.False(json.RootElement.GetProperty("found").GetBoolean());
        Assert.Contains("No complaints found", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetComplaintsAsync_ReturnsComplaints_WhenFound()
    {
        // Arrange
        var complaints = new ComplaintResponse
        {
            Count = 1,
            Results = [new ComplaintInfo { Components = "ENGINE", Summary = "Engine stalls", Crash = true, Fire = false }]
        };
        _mockNhtsaService.Setup(s => s.GetComplaintsAsync("Chevrolet", "Equinox", 2019, default))
            .ReturnsAsync(complaints);

        // Act
        var result = await _tools.GetComplaintsAsync("Chevrolet", "Equinox", 2019);
        var json = JsonDocument.Parse(result);

        // Assert
        Assert.True(json.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("count").GetInt32());
        var firstComplaint = json.RootElement.GetProperty("complaints")[0];
        Assert.True(firstComplaint.GetProperty("crash").GetBoolean());
        Assert.False(firstComplaint.GetProperty("fire").GetBoolean());
    }

    #endregion

    #region GetSafetyRatingAsync Tests

    [Fact]
    public async Task GetSafetyRatingAsync_ReturnsNotFound_WhenNoRating()
    {
        // Arrange
        _mockNhtsaService.Setup(s => s.GetSafetyRatingAsync("Lucid", "Air", 2024, default))
            .ReturnsAsync((SafetyRating?)null);

        // Act
        var result = await _tools.GetSafetyRatingAsync("Lucid", "Air", 2024);
        var json = JsonDocument.Parse(result);

        // Assert
        Assert.False(json.RootElement.GetProperty("found").GetBoolean());
        Assert.Contains("No safety rating found", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetSafetyRatingAsync_ReturnsRating_WhenFound()
    {
        // Arrange
        var rating = new SafetyRating
        {
            VehicleId = 12345,
            VehicleDescription = "2024 Toyota Camry SE",
            OverallRating = "5",
            OverallFrontCrashRating = "5",
            OverallSideCrashRating = "5",
            RolloverRating = "4",
            ForwardCollisionWarning = "Standard",
            LaneDepartureWarning = "Standard"
        };
        _mockNhtsaService.Setup(s => s.GetSafetyRatingAsync("Toyota", "Camry", 2024, default))
            .ReturnsAsync(rating);

        // Act
        var result = await _tools.GetSafetyRatingAsync("Toyota", "Camry", 2024);
        var json = JsonDocument.Parse(result);

        // Assert
        Assert.True(json.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal("5", json.RootElement.GetProperty("overallRating").GetString());
        Assert.Equal("5", json.RootElement.GetProperty("frontCrashRating").GetString());
        Assert.Equal("4", json.RootElement.GetProperty("rolloverRating").GetString());
        Assert.Equal("Standard", json.RootElement.GetProperty("forwardCollisionWarning").GetString());
    }

    #endregion
}

