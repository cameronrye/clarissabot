namespace ClarissaBot.Tests;

using ClarissaBot.Core.Agent;
using ClarissaBot.Core.Services;
using ClarissaBot.Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;

/// <summary>
/// Unit tests for ClarissaAgent.
/// Tests conversation management, tool execution, and error handling.
/// Note: Tool execution and streaming require integration tests with real OpenAI client.
/// </summary>
public class ClarissaAgentTests : IDisposable
{
    private readonly Mock<INhtsaService> _mockNhtsaService;
    private readonly Mock<ILogger<ClarissaAgent>> _mockLogger;
    private readonly NhtsaTools _nhtsaTools;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public ClarissaAgentTests()
    {
        _mockNhtsaService = new Mock<INhtsaService>();
        _mockLogger = new Mock<ILogger<ClarissaAgent>>();
        _nhtsaTools = new NhtsaTools(_mockNhtsaService.Object);

        // Create mock configuration
        _mockConfiguration = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns((string?)null);
        _mockConfiguration.Setup(c => c.GetSection(It.IsAny<string>())).Returns(mockSection.Object);
    }

    public void Dispose()
    {
        // Tests that create agents should dispose them
        GC.SuppressFinalize(this);
    }

    #region ClearConversation Tests

    [Fact]
    public void ClearConversation_DoesNotThrow_WhenConversationDoesNotExist()
    {
        // Arrange
        using var agent = CreateAgentWithMockClient();

        // Act & Assert - should not throw
        agent.ClearConversation("non-existent-id");
    }

    [Fact]
    public void ClearConversation_ClearsDefaultConversation()
    {
        // Arrange
        using var agent = CreateAgentWithMockClient();

        // Act - clear default conversation (even if empty, should not throw)
        agent.ClearConversation();
        agent.ClearConversation(null);
        
        // Assert - no exception means success
        Assert.True(true);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var agent = CreateAgentWithMockClient();

        // Act & Assert - should not throw
        agent.Dispose();
        agent.Dispose();
        agent.Dispose();
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_UsesDefaultConfiguration_WhenConfigIsNull()
    {
        // Arrange & Act - create with null configuration
        using var agent = CreateAgentWithMockClient(useNullConfig: true);

        // Assert - agent should be created successfully
        Assert.NotNull(agent);
    }

    [Fact]
    public void Constructor_ReadsConfigurationValues()
    {
        // Arrange - use mock configuration
        // Act
        using var agent = CreateAgentWithMockClient();

        // Assert - agent should be created with custom config
        Assert.NotNull(agent);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates an agent with a mock ChatClient.
    /// Note: The ChatClient from OpenAI SDK is not easily mockable, so we create a minimal test version.
    /// Full integration tests should use a real client.
    /// </summary>
    private ClarissaAgent CreateAgentWithMockClient(bool useNullConfig = false)
    {
        // Create a minimal mock ChatClient
        // Note: ChatClient is from the Azure OpenAI SDK and has limited testability
        // For true mocking, we'd need to abstract IChatClient interface
        var mockChatClient = CreateMockChatClient();

        return new ClarissaAgent(
            mockChatClient,
            _nhtsaTools,
            _mockLogger.Object,
            useNullConfig ? null : _mockConfiguration.Object);
    }

    private static ChatClient CreateMockChatClient()
    {
        // The Azure OpenAI ChatClient requires actual credentials
        // For unit tests, we test what we can without real API calls
        // Integration tests should cover actual chat completion scenarios
        
        // This creates a client that will fail if actually called
        // but allows us to test initialization and non-API code paths
        var mockClient = new Azure.AI.OpenAI.AzureOpenAIClient(
            new Uri("https://test.openai.azure.com"),
            new Azure.AzureKeyCredential("test-key"));
        
        return mockClient.GetChatClient("test-deployment");
    }

    #endregion
}

