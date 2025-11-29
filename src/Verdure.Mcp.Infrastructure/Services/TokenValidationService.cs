using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Verdure.Mcp.Domain.Entities;
using Verdure.Mcp.Infrastructure.Data;

namespace Verdure.Mcp.Infrastructure.Services;

/// <summary>
/// Token validation settings
/// </summary>
public class TokenValidationSettings
{
    public const string SectionName = "Authentication";
    
    /// <summary>
    /// Default token expiration in days
    /// </summary>
    public int DefaultTokenExpirationDays { get; set; } = 30;

    /// <summary>
    /// Default daily image generation limit per user
    /// </summary>
    public int DefaultDailyImageLimit { get; set; } = 10;
}

/// <summary>
/// Interface for token validation service
/// </summary>
public interface ITokenValidationService
{
    Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<ApiToken?> GetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<string> CreateTokenAsync(string name, DateTime? expiresAt = null, CancellationToken cancellationToken = default);
    Task<string> CreateUserTokenAsync(string userId, string name, CancellationToken cancellationToken = default);
    Task<List<ApiToken>> GetUserTokensAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> RevokeTokenAsync(Guid tokenId, string userId, CancellationToken cancellationToken = default);
    Task<bool> CheckImageGenerationLimitAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> IncrementImageCountAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for validating API tokens using secure PBKDF2 hashing
/// </summary>
public class TokenValidationService : ITokenValidationService
{
    private readonly McpDbContext _dbContext;
    private readonly ILogger<TokenValidationService> _logger;
    private readonly TokenValidationSettings _settings;
    
    // PBKDF2 parameters for secure token hashing
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public TokenValidationService(
        McpDbContext dbContext, 
        ILogger<TokenValidationService> logger,
        IOptions<TokenValidationSettings> settings)
    {
        _dbContext = dbContext;
        _logger = logger;
        _settings = settings.Value;
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
        // Get all active tokens and verify against each using constant-time comparison
        var tokens = await _dbContext.ApiTokens
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var apiToken in tokens)
        {
            if (VerifyHash(token, apiToken.TokenHash))
            {
                return apiToken;
            }
        }

        return null;
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
            ExpiresAt = expiresAt,
            DailyImageLimit = _settings.DefaultDailyImageLimit
        };

        _dbContext.ApiTokens.Add(apiToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new API token: {TokenName}", name);

        return token;
    }

    public async Task<string> CreateUserTokenAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        // Generate a random token
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var token = Convert.ToBase64String(tokenBytes);
        var tokenHash = ComputeHash(token);

        // Calculate expiration based on settings
        var expiresAt = DateTime.UtcNow.AddDays(_settings.DefaultTokenExpirationDays);

        var apiToken = new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            DailyImageLimit = _settings.DefaultDailyImageLimit
        };

        _dbContext.ApiTokens.Add(apiToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new user API token: {TokenName} for user {UserId}", name, userId);

        return token;
    }

    public async Task<List<ApiToken>> GetUserTokensAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeTokenAsync(Guid tokenId, string userId, CancellationToken cancellationToken = default)
    {
        var token = await _dbContext.ApiTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);

        if (token == null)
        {
            return false;
        }

        token.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Revoked token {TokenId} for user {UserId}", tokenId, userId);
        return true;
    }

    public async Task<bool> CheckImageGenerationLimitAsync(string token, CancellationToken cancellationToken = default)
    {
        var apiToken = await GetTokenAsync(token, cancellationToken);
        
        if (apiToken == null)
        {
            return false;
        }

        // Reset daily count if it's a new day
        var today = DateTime.UtcNow.Date;
        if (apiToken.LastImageCountReset == null || apiToken.LastImageCountReset.Value.Date < today)
        {
            apiToken.TodayImageCount = 0;
            apiToken.LastImageCountReset = today;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return apiToken.TodayImageCount < apiToken.DailyImageLimit;
    }

    public async Task<bool> IncrementImageCountAsync(string token, CancellationToken cancellationToken = default)
    {
        var apiToken = await GetTokenAsync(token, cancellationToken);
        
        if (apiToken == null)
        {
            return false;
        }

        // Reset daily count if it's a new day
        var today = DateTime.UtcNow.Date;
        if (apiToken.LastImageCountReset == null || apiToken.LastImageCountReset.Value.Date < today)
        {
            apiToken.TodayImageCount = 0;
            apiToken.LastImageCountReset = today;
        }

        if (apiToken.TodayImageCount >= apiToken.DailyImageLimit)
        {
            return false;
        }

        apiToken.TodayImageCount++;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Computes a secure hash of the token using PBKDF2 with a random salt
    /// </summary>
    private static string ComputeHash(string token)
    {
        // Generate a random salt
        var salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Compute the hash using PBKDF2
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(token),
            salt,
            Iterations,
            HashAlgorithm,
            HashSize);

        // Combine salt and hash for storage (salt:hash)
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a token against a stored hash using constant-time comparison
    /// </summary>
    private static bool VerifyHash(string token, string storedHash)
    {
        try
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);

            // Compute the hash of the provided token
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(token),
                salt,
                Iterations,
                HashAlgorithm,
                HashSize);

            // Use constant-time comparison to prevent timing attacks
            return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
