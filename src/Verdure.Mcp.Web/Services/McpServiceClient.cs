using System.Net.Http.Json;
using Verdure.Mcp.Shared.Models;

namespace Verdure.Mcp.Web.Services;

public interface IMcpServiceClient
{
    Task<List<McpServiceDto>> GetServicesAsync(string? category = null);
    Task<PagedResult<McpServiceDto>> GetServicesPagedAsync(int page, int pageSize, string? category = null);
    Task<PagedResult<McpServiceDto>> GetServicesPagedAdminAsync(int page, int pageSize, string? category = null);
    Task<McpServiceDto?> GetServiceAsync(Guid id);
    Task<List<McpCategoryDto>> GetCategoriesAsync();
    Task<McpServiceDto> CreateServiceAsync(McpServiceRequest request);
    Task<McpServiceDto> UpdateServiceAsync(Guid id, McpServiceRequest request);
    Task DeleteServiceAsync(Guid id);
}

public class McpServiceClient : IMcpServiceClient
{
    private readonly HttpClient _httpClient;

    public McpServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<McpServiceDto>> GetServicesAsync(string? category = null)
    {
        var url = "api/mcp-services";
        if (!string.IsNullOrEmpty(category))
        {
            url += $"?category={Uri.EscapeDataString(category)}";
        }
        
        var response = await _httpClient.GetFromJsonAsync<ApiResponse<List<McpServiceDto>>>(url);
        return response?.Data ?? [];
    }

    public async Task<PagedResult<McpServiceDto>> GetServicesPagedAsync(int page, int pageSize, string? category = null)
    {
        var url = $"api/mcp-services/paged?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(category))
        {
            url += $"&category={Uri.EscapeDataString(category)}";
        }
        
        var response = await _httpClient.GetFromJsonAsync<ApiResponse<PagedResult<McpServiceDto>>>(url);
        return response?.Data ?? new PagedResult<McpServiceDto>();
    }

    public async Task<PagedResult<McpServiceDto>> GetServicesPagedAdminAsync(int page, int pageSize, string? category = null)
    {
        var url = $"api/mcp-services/admin/paged?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(category))
        {
            url += $"&category={Uri.EscapeDataString(category)}";
        }
        
        var response = await _httpClient.GetFromJsonAsync<ApiResponse<PagedResult<McpServiceDto>>>(url);
        return response?.Data ?? new PagedResult<McpServiceDto>();
    }

    public async Task<McpServiceDto?> GetServiceAsync(Guid id)
    {
        var response = await _httpClient.GetFromJsonAsync<ApiResponse<McpServiceDto>>($"api/mcp-services/{id}");
        return response?.Data;
    }

    public async Task<List<McpCategoryDto>> GetCategoriesAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<ApiResponse<List<McpCategoryDto>>>("api/mcp-services/categories");
        return response?.Data ?? [];
    }

    public async Task<McpServiceDto> CreateServiceAsync(McpServiceRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/mcp-services", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<McpServiceDto>>();
        return result?.Data ?? throw new InvalidOperationException("Failed to create service");
    }

    public async Task<McpServiceDto> UpdateServiceAsync(Guid id, McpServiceRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/mcp-services/{id}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<McpServiceDto>>();
        return result?.Data ?? throw new InvalidOperationException("Failed to update service");
    }

    public async Task DeleteServiceAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/mcp-services/{id}");
        response.EnsureSuccessStatusCode();
    }
}
