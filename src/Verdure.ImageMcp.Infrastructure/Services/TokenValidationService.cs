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
/// Service for validating API tokens using secure PBKDF2 hashing
/// </summary>
public class TokenValidationService : ITokenValidationService
{
    private readonly ImageMcpDbContext _dbContext;
    private readonly ILogger<TokenValidationService> _logger;
    
    // PBKDF2 parameters for secure token hashing
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

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
            ExpiresAt = expiresAt
        };

        _dbContext.ApiTokens.Add(apiToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new API token: {TokenName}", name);

        return token;
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
