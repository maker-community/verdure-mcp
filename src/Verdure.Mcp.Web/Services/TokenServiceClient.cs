using System.Net.Http.Json;
using Verdure.Mcp.Shared.Models;

namespace Verdure.Mcp.Web.Services;

public interface ITokenServiceClient
{
    Task<List<ApiTokenDto>> GetUserTokensAsync();
    Task<CreateTokenResponse> CreateTokenAsync(CreateTokenRequest request);
    Task RevokeTokenAsync(Guid tokenId);
}

public class TokenServiceClient : ITokenServiceClient
{
    private readonly HttpClient _httpClient;

    public TokenServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ApiTokenDto>> GetUserTokensAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<ApiResponse<List<ApiTokenDto>>>("api/tokens");
        return response?.Data ?? [];
    }

    public async Task<CreateTokenResponse> CreateTokenAsync(CreateTokenRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tokens", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CreateTokenResponse>>();
        return result?.Data ?? throw new InvalidOperationException("Failed to create token");
    }

    public async Task RevokeTokenAsync(Guid tokenId)
    {
        var response = await _httpClient.DeleteAsync($"api/tokens/{tokenId}");
        response.EnsureSuccessStatusCode();
    }
}
