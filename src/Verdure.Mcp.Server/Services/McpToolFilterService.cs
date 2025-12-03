using ModelContextProtocol.Server;

namespace Verdure.Mcp.Server.Services;

/// <summary>
/// Service for filtering MCP tools based on categories
/// </summary>
public class McpToolFilterService
{
    private readonly ILogger<McpToolFilterService> _logger;
    
    // Tool category definitions - easy to extend
    private readonly Dictionary<string, Func<string, bool>> _categoryFilters = new()
    {
        ["image"] = toolName => toolName.Contains("image", StringComparison.OrdinalIgnoreCase),
        ["email"] = toolName => toolName.Contains("email", StringComparison.OrdinalIgnoreCase),
        ["debug"] = toolName => toolName.Contains("debug", StringComparison.OrdinalIgnoreCase),
        ["all"] = _ => true // Accept all tools
    };
    
    public McpToolFilterService(ILogger<McpToolFilterService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Register a new tool category filter
    /// </summary>
    /// <param name="category">Category name (e.g., "document", "database")</param>
    /// <param name="filter">Function to determine if a tool belongs to this category</param>
    public void RegisterCategory(string category, Func<string, bool> filter)
    {
        _categoryFilters[category.ToLower()] = filter;
        _logger.LogInformation("Registered new tool category: {Category}", category);
    }
    
    /// <summary>
    /// Get all available categories
    /// </summary>
    public IEnumerable<string> GetAvailableCategories() => _categoryFilters.Keys;
    
    /// <summary>
    /// Filter tools based on the specified category
    /// </summary>
    /// <param name="allTools">All available tools</param>
    /// <param name="category">Target category (e.g., "image", "email", "all")</param>
    /// <returns>Filtered list of tools</returns>
    public List<McpServerTool> FilterTools(IEnumerable<McpServerTool> allTools, string category)
    {
        var normalizedCategory = category?.ToLower() ?? "all";
        
        _logger.LogInformation("Filtering tools for category: {Category}", normalizedCategory);
        
        // Get the filter function for this category
        if (!_categoryFilters.TryGetValue(normalizedCategory, out var filterFunc))
        {
            _logger.LogWarning("Unknown category '{Category}', defaulting to 'all'", normalizedCategory);
            filterFunc = _categoryFilters["all"];
            normalizedCategory = "all";
        }
        
        var toolList = allTools.ToList();
        
        if (toolList.Count == 0)
        {
            _logger.LogWarning("No tools registered in the MCP server");
            return toolList;
        }
        
        // Apply filter
        var filtered = toolList.Where(tool => filterFunc(tool.ProtocolTool.Name)).ToList();
        
        _logger.LogInformation(
            "Filtered tools: {FilteredCount}/{TotalCount} tools for category '{Category}'",
            filtered.Count,
            toolList.Count,
            normalizedCategory);
        
        // Log which tools were selected
        if (filtered.Count > 0)
        {
            var toolNames = string.Join(", ", filtered.Select(t => t.ProtocolTool.Name));
            _logger.LogDebug("Selected tools: {ToolNames}", toolNames);
        }
        
        return filtered;
    }
    
    /// <summary>
    /// Apply filtered tools to MCP options
    /// </summary>
    /// <param name="mcpOptions">MCP server options</param>
    /// <param name="filteredTools">Filtered tools to apply</param>
    public void ApplyFilteredTools(McpServerOptions mcpOptions, List<McpServerTool> filteredTools)
    {
        if (mcpOptions.ToolCollection == null)
        {
            _logger.LogWarning("ToolCollection is null, cannot apply filtered tools");
            return;
        }
        
        // Clear existing tools
        mcpOptions.ToolCollection.Clear();
        
        // Add filtered tools
        foreach (var tool in filteredTools)
        {
            mcpOptions.ToolCollection.Add(tool);
        }
        
        _logger.LogInformation("Applied {Count} filtered tools to session", filteredTools.Count);
    }
}
