using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace Verdure.Mcp.Server.Tools;

/// <summary>
/// MCP 调试工具，用于打印请求头等调试信息
/// </summary>
[McpServerToolType]
public partial class DebugTool
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DebugTool> _logger;

    public DebugTool(
        IHttpContextAccessor httpContextAccessor,
        ILogger<DebugTool> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// 打印当前请求的所有请求头信息，便于调试
    /// </summary>
    /// <returns>包含所有请求头的响应对象</returns>
    [McpServerTool(Name = "debug_print_headers")]
    [Description("打印当前请求的所有请求头信息，用于调试")]
    public partial Task<DebugHeadersResponse> PrintRequestHeaders()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext == null)
        {
            _logger.LogWarning("无法访问 HttpContext");
            return Task.FromResult(new DebugHeadersResponse
            {
                Success = false,
                Message = "无法访问 HttpContext",
                Headers = new Dictionary<string, string>()
            });
        }

        var headers = new Dictionary<string, string>();
        var sb = new StringBuilder();
        sb.AppendLine("=== 请求头信息 ===");
        
        foreach (var header in httpContext.Request.Headers)
        {
            var headerValue = string.Join(", ", header.Value.ToArray());
            headers[header.Key] = headerValue;
            sb.AppendLine($"{header.Key}: {headerValue}");
        }

        var headersInfo = sb.ToString();
        _logger.LogInformation("请求头信息:\n{Headers}", headersInfo);

        return Task.FromResult(new DebugHeadersResponse
        {
            Success = true,
            Message = "请求头信息已打印",
            Headers = headers,
            FormattedHeaders = headersInfo
        });
    }

    /// <summary>
    /// 打印当前请求的详细信息，包括请求头、方法、路径等
    /// </summary>
    /// <returns>包含请求详细信息的响应对象</returns>
    [McpServerTool(Name = "debug_print_request")]
    [Description("打印当前请求的详细信息，包括请求头、方法、路径等")]
    public partial Task<DebugRequestResponse> PrintRequestDetails()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext == null)
        {
            _logger.LogWarning("无法访问 HttpContext");
            return Task.FromResult(new DebugRequestResponse
            {
                Success = false,
                Message = "无法访问 HttpContext"
            });
        }

        var request = httpContext.Request;
        var headers = new Dictionary<string, string>();
        
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value.ToArray());
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== 请求详细信息 ===");
        sb.AppendLine($"方法: {request.Method}");
        sb.AppendLine($"路径: {request.Path}");
        sb.AppendLine($"查询字符串: {request.QueryString}");
        sb.AppendLine($"协议: {request.Protocol}");
        sb.AppendLine($"Scheme: {request.Scheme}");
        sb.AppendLine($"Host: {request.Host}");
        sb.AppendLine($"ContentType: {request.ContentType}");
        sb.AppendLine($"ContentLength: {request.ContentLength}");
        sb.AppendLine();
        sb.AppendLine("请求头:");
        foreach (var header in headers)
        {
            sb.AppendLine($"  {header.Key}: {header.Value}");
        }

        var requestInfo = sb.ToString();
        _logger.LogInformation("请求详细信息:\n{RequestInfo}", requestInfo);

        return Task.FromResult(new DebugRequestResponse
        {
            Success = true,
            Message = "请求详细信息已打印",
            Method = request.Method,
            Path = request.Path.ToString(),
            QueryString = request.QueryString.ToString(),
            Protocol = request.Protocol,
            Scheme = request.Scheme,
            Host = request.Host.ToString(),
            ContentType = request.ContentType,
            ContentLength = request.ContentLength,
            Headers = headers,
            FormattedRequest = requestInfo
        });
    }
}

/// <summary>
/// 调试请求头响应模型
/// </summary>
public class DebugHeadersResponse
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? FormattedHeaders { get; set; }
}

/// <summary>
/// 调试请求详情响应模型
/// </summary>
public class DebugRequestResponse
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }
    public string? QueryString { get; set; }
    public string? Protocol { get; set; }
    public string? Scheme { get; set; }
    public string? Host { get; set; }
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? FormattedRequest { get; set; }
}
