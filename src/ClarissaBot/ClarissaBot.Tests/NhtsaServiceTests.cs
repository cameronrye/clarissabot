namespace ClarissaBot.Tests;

using System.Net;
using System.Text.Json;
using ClarissaBot.Core.Models;
using ClarissaBot.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

public class NhtsaServiceTests
{
    private readonly Mock<ILogger<NhtsaService>> _mockLogger;

    public NhtsaServiceTests()
    {
        _mockLogger = new Mock<ILogger<NhtsaService>>();
    }

    private HttpClient CreateMockHttpClient(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
            });

        return new HttpClient(mockHandler.Object);
    }

    [Fact]
    public async Task GetRecallsAsync_ReturnsRecalls_WhenApiReturnsData()
    {
        // Arrange
        var expectedResponse = new RecallResponse
        {
            Count = 2,
            Results = 
            [
                new RecallInfo { CampaignNumber = "12V456000", Component = "AIR BAGS", Summary = "Test recall 1" },
                new RecallInfo { CampaignNumber = "12V789000", Component = "BRAKES", Summary = "Test recall 2" }
            ]
        };
        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(expectedResponse));
        var service = new NhtsaService(httpClient, _mockLogger.Object);

        // Act
        var result = await service.GetRecallsAsync("Acura", "RDX", 2012);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.NotNull(result.Results);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal("12V456000", result.Results[0].CampaignNumber);
    }

    [Fact]
    public async Task GetRecallsAsync_ReturnsEmptyList_WhenApiReturnsNoData()
    {
        // Arrange
        var expectedResponse = new RecallResponse { Count = 0, Results = [] };
        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(expectedResponse));
        var service = new NhtsaService(httpClient, _mockLogger.Object);

        // Act
        var result = await service.GetRecallsAsync("Unknown", "Car", 2020);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.NotNull(result.Results);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task GetComplaintsAsync_ReturnsComplaints_WhenApiReturnsData()
    {
        // Arrange
        var expectedResponse = new ComplaintResponse
        {
            Count = 1,
            Results = 
            [
                new ComplaintInfo
                {
                    OdiNumber = 11111111,
                    Components = "ENGINE",
                    Summary = "Engine issues",
                    Crash = false,
                    Fire = false
                }
            ]
        };
        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(expectedResponse));
        var service = new NhtsaService(httpClient, _mockLogger.Object);

        // Act
        var result = await service.GetComplaintsAsync("Tesla", "Model 3", 2020);

        // Assert
        Assert.Equal(1, result.Count);
        Assert.NotNull(result.Results);
        Assert.Single(result.Results);
        Assert.Equal("ENGINE", result.Results[0].Components);
    }

    [Fact]
    public async Task GetRecallsAsync_ReturnsEmptyResponse_WhenApiThrowsException()
    {
        // Arrange - create a client that will throw
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new NhtsaService(httpClient, _mockLogger.Object);

        // Act
        var result = await service.GetRecallsAsync("Test", "Car", 2020);

        // Assert
        Assert.Equal(0, result.Count);
        Assert.NotNull(result.Results);
        Assert.Empty(result.Results);
    }
}

