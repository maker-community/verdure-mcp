using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Verdure.ImageMcp.Domain.Entities;
using Verdure.ImageMcp.Infrastructure.Data;

namespace Verdure.ImageMcp.Infrastructure.Services;

/// <summary>
/// Interface for token validation service
/// </summary>
public interface ITokenValidationService
{
    Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<ApiToken?> GetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<string> CreateTokenAsync(string name, DateTime? expiresAt = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for validating API tokens
/// </summary>
public class TokenValidationService : ITokenValidationService
{
    private readonly ImageMcpDbContext _dbContext;
    private readonly ILogger<TokenValidationService> _logger;

    public TokenValidationService(ImageMcpDbContext dbContext, ILogger<TokenValidationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var apiToken = await GetTokenAsync(token, cancellationToken);
        
        if (apiToken == null)
        {
            _logger.LogWarning("Token not found or invalid");
            return false;
        }

        if (!apiToken.IsActive)
        {
            _logger.LogWarning("Token is not active: {TokenName}", apiToken.Name);
            return false;
        }

        if (apiToken.ExpiresAt.HasValue && apiToken.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Token has expired: {TokenName}", apiToken.Name);
            return false;
        }

        // Update last used timestamp
        apiToken.LastUsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<ApiToken?> GetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeHash(token);
        return await _dbContext.ApiTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<string> CreateTokenAsync(string name, DateTime? expiresAt = null, CancellationToken cancellationToken = default)
    {
        // Generate a random token
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var token = Convert.ToBase64String(tokenBytes);
        var tokenHash = ComputeHash(token);

        var apiToken = new ApiToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        _dbContext.ApiTokens.Add(apiToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new API token: {TokenName}", name);

        return token;
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
