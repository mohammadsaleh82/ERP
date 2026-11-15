using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ERP.BuildingBlocks.Security;
using ERP.Modules.Identity.Domain;
using ERP.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Modules.Identity.Application.Services;

public interface ITokenService
{
    string GenerateAccessToken(AppUser user, Guid tenantId, Guid companyId, Guid? branchId, IEnumerable<string> permissions, IEnumerable<string> roles);
    Task<string> GenerateAndSaveRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(string NewAccessToken, string NewRefreshToken)?> RotateRefreshTokenAsync(string refreshToken, Guid tenantId, Guid companyId, Guid? branchId, IEnumerable<string> permissions, IEnumerable<string> roles, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public class TokenService : ITokenService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public TokenService(IdentityDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public string GenerateAccessToken(
        AppUser user,
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        IEnumerable<string> permissions,
        IEnumerable<string> roles)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "ERP_Fallback_Development_Secret_Key_At_Least_32_Bytes_Long!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.FullName),
            new(SecurityConstants.TenantIdClaim, tenantId.ToString()),
            new(SecurityConstants.CompanyIdClaim, companyId.ToString()),
        };

        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            claims.Add(new Claim(SecurityConstants.BranchIdClaim, branchId.Value.ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim(SecurityConstants.PermissionsClaim, permission));
        }

        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var mins) ? mins : 15;

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "ERP.Api",
            audience: _configuration["Jwt:Audience"] ?? "ERP.Client",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateAndSaveRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateSecureRandomToken();
        var tokenHash = HashToken(rawToken);

        var expiryDays = int.TryParse(_configuration["Jwt:RefreshTokenExpiryDays"], out var days) ? days : 7;

        var tokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(expiryDays),
        };

        _dbContext.RefreshTokens.Add(tokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    public async Task<(string NewAccessToken, string NewRefreshToken)?> RotateRefreshTokenAsync(
        string rawRefreshToken,
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        IEnumerable<string> permissions,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(rawRefreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || existingToken.User is null)
        {
            return null;
        }

        // Reuse detection: if revoked, compromised token presented! Revoke all tokens for this user!
        if (existingToken.IsRevoked)
        {
            var activeTokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == existingToken.UserId && t.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var t in activeTokens)
            {
                t.RevokedAtUtc = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (existingToken.IsExpired || !existingToken.IsActive)
        {
            return null;
        }

        // Generate new pair
        var newRawRefreshToken = GenerateSecureRandomToken();
        var newTokenHash = HashToken(newRawRefreshToken);
        var expiryDays = int.TryParse(_configuration["Jwt:RefreshTokenExpiryDays"], out var days) ? days : 7;

        existingToken.RevokedAtUtc = DateTimeOffset.UtcNow;
        existingToken.ReplacedByTokenHash = newTokenHash;

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingToken.UserId,
            TokenHash = newTokenHash,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(expiryDays),
        };

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var newAccessToken = GenerateAccessToken(
            existingToken.User,
            tenantId,
            companyId,
            branchId,
            permissions,
            roles);

        return (newAccessToken, newRawRefreshToken);
    }

    public async Task RevokeRefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(rawRefreshToken);
        var token = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (token is not null && !token.IsRevoked)
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateSecureRandomToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
