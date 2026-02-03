namespace Verdure.Mcp.Server.Settings;

/// <summary>
/// Settings for Azure OpenAI Chat configuration (dedicated for AI Group Chat)
/// </summary>
public class AzureOpenAIChatSettings
{
    public const string SectionName = "AzureOpenAIChat";
    
    /// <summary>
    /// Azure OpenAI endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure OpenAI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Deployment name for chat models (e.g., gpt-4o-mini, gpt-4o, gpt-4-turbo)
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4o-mini";
    
    /// <summary>
    /// API version
    /// </summary>
    public string ApiVersion { get; set; } = "2024-02-01";
}
