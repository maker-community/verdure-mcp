namespace Verdure.Mcp.Domain.Entities;

/// <summary>
/// Represents an AI agent's profile configuration
/// </summary>
public class AgentProfile
{
    /// <summary>
    /// Unique identifier for the agent profile
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Agent ID used in the Agent Framework
    /// </summary>
    public required string AgentId { get; set; }

    /// <summary>
    /// Display name of the agent
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Avatar URL for the agent
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// Personality description of the agent
    /// </summary>
    public required string Personality { get; set; }

    /// <summary>
    /// System prompt for the agent
    /// </summary>
    public required string SystemPrompt { get; set; }

    /// <summary>
    /// List of capabilities (e.g., "生图", "音乐", "闲聊")
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// When the agent profile was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
