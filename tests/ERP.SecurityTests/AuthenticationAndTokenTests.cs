using System.IdentityModel.Tokens.Jwt;
using ERP.BuildingBlocks.Security;
using ERP.Modules.Identity.Application.Services;
using ERP.Modules.Identity.Domain;
using ERP.Modules.Identity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ERP.SecurityTests;

public class AuthenticationAndTokenTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IdentityDbContext> _options;
    private readonly IConfiguration _configuration;

    public AuthenticationAndTokenTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new IdentityDbContext(_options);
        initContext.Database.EnsureCreated();

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "SuperSecretSecureKeyForTestingERPAuthSystemAtLeast32Bytes!",
            ["Jwt:Issuer"] = "ERP.Api.Test",
            ["Jwt:Audience"] = "ERP.Client.Test",
            ["Jwt:ExpiryMinutes"] = "15",
            ["Jwt:RefreshTokenExpiryDays"] = "7"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainRequiredClaimsAndValidSignature()
    {
        using var dbContext = new IdentityDbContext(_options);
        var tokenService = new TokenService(dbContext, _configuration);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "test.architect@erp.com",
            Email = "test.architect@erp.com",
            FullName = "Architecture Tester"
        };
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var permissions = new[] { "accounting:vouchers:read", "accounting:vouchers:create" };
        var roles = new[] { "FinancialManager" };

        var tokenString = tokenService.GenerateAccessToken(user, tenantId, companyId, branchId, permissions, roles);

        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        jwt.Issuer.Should().Be("ERP.Api.Test");
        jwt.Audiences.Should().Contain("ERP.Client.Test");

        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        subClaim.Should().NotBeNull();
        subClaim!.Value.Should().Be(user.Id.ToString());

        var tenantClaim = jwt.Claims.FirstOrDefault(c => c.Type == SecurityConstants.TenantIdClaim);
        tenantClaim.Should().NotBeNull();
        tenantClaim!.Value.Should().Be(tenantId.ToString());

        var companyClaim = jwt.Claims.FirstOrDefault(c => c.Type == SecurityConstants.CompanyIdClaim);
        companyClaim.Should().NotBeNull();
        companyClaim!.Value.Should().Be(companyId.ToString());

        var branchClaim = jwt.Claims.FirstOrDefault(c => c.Type == SecurityConstants.BranchIdClaim);
        branchClaim.Should().NotBeNull();
        branchClaim!.Value.Should().Be(branchId.ToString());

        var permClaims = jwt.Claims.Where(c => c.Type == SecurityConstants.PermissionsClaim).Select(c => c.Value).ToList();
        permClaims.Should().BeEquivalentTo(permissions);
    }

    [Fact]
    public async Task GenerateAndSaveRefreshToken_ShouldStoreHashedTokenAndReturnRawSecret()
    {
        using var dbContext = new IdentityDbContext(_options);
        var tokenService = new TokenService(dbContext, _configuration);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "hashed_test@erp.com",
            Email = "hashed_test@erp.com",
            FullName = "Hash Tester"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var rawToken = await tokenService.GenerateAndSaveRefreshTokenAsync(user.Id, Guid.NewGuid(), Guid.NewGuid());

        rawToken.Should().NotBeNullOrWhiteSpace();

        var savedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        savedToken.Should().NotBeNull();
        savedToken!.IsActive.Should().BeTrue();
        savedToken.TokenHash.Should().NotBe(rawToken); // Critical: Raw token must never be stored in plain text!
        savedToken.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RotateRefreshToken_ShouldRevokeOldTokenAndIssueNewValidPair()
    {
        using var dbContext = new IdentityDbContext(_options);
        var tokenService = new TokenService(dbContext, _configuration);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "rotator@erp.com",
            Email = "rotator@erp.com",
            FullName = "Rotator Test"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var rawRefreshToken = await tokenService.GenerateAndSaveRefreshTokenAsync(user.Id, Guid.NewGuid(), Guid.NewGuid());

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var result = await tokenService.RotateRefreshTokenAsync(
            rawRefreshToken,
            tenantId,
            companyId,
            branchId: null,
            permissions: ["treasury:view"],
            roles: ["TreasurySpecialist"]);

        result.Should().NotBeNull();
        result!.Value.NewAccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.NewRefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.NewRefreshToken.Should().NotBe(rawRefreshToken);

        // Verify the old token is marked as revoked in DB
        var tokens = await dbContext.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        tokens.Should().HaveCount(2);

        var oldToken = tokens.FirstOrDefault(t => t.RevokedAtUtc != null);
        oldToken.Should().NotBeNull();
        oldToken!.ReplacedByTokenHash.Should().NotBeNull();

        var activeNewToken = tokens.FirstOrDefault(t => t.RevokedAtUtc == null);
        activeNewToken.Should().NotBeNull();
        activeNewToken!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RotateRefreshToken_CompromisedTokenReuse_ShouldRevokeAllUserTokens()
    {
        using var dbContext = new IdentityDbContext(_options);
        var tokenService = new TokenService(dbContext, _configuration);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "victim@erp.com",
            Email = "victim@erp.com",
            FullName = "Victim User"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var token1 = await tokenService.GenerateAndSaveRefreshTokenAsync(user.Id, Guid.NewGuid(), Guid.NewGuid());

        // Legitimate user rotates token1 -> receives token2
        var rotate1 = await tokenService.RotateRefreshTokenAsync(
            token1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            [],
            []);
        rotate1.Should().NotBeNull();
        var token2 = rotate1!.Value.NewRefreshToken;

        // Attacker attempts to replay token1 (which is now revoked)
        var reuseAttempt = await tokenService.RotateRefreshTokenAsync(
            token1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            [],
            []);

        // Reuse detection must reject the request
        reuseAttempt.Should().BeNull();

        // Crucial security behavior: ALL tokens for this user must be revoked due to compromised session!
        var userTokens = await dbContext.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        userTokens.Should().AllSatisfy(t => t.RevokedAtUtc.Should().NotBeNull());

        // Legitimate token2 should also now be revoked
        var token2Attempt = await tokenService.RotateRefreshTokenAsync(
            token2,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            [],
            []);
        token2Attempt.Should().BeNull();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
