using ERP.BuildingBlocks.Results;
using ERP.BuildingBlocks.Security;
using ERP.Api.Middleware;
using ERP.Modules.Identity.Application.Services;
using ERP.Modules.Identity.Domain;
using ERP.Modules.Tenancy.Contracts.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

public sealed record LoginRequest(string Email, string Password, Guid? TenantId = null, Guid? CompanyId = null);
public sealed record LoginResponse(string AccessToken, string Email, string FullName, Guid TenantId, Guid CompanyId, IReadOnlyList<string> Permissions);
public sealed record RefreshTokenResponse(string AccessToken, Guid TenantId, Guid CompanyId, IReadOnlyList<string> Permissions);

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ITenancyMembershipValidator _membershipValidator;
    private readonly ITenancyModuleApi _tenancyModuleApi;
    private readonly ICurrentUserContext _currentUserContext;

    public AuthController(
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        ITenancyMembershipValidator membershipValidator,
        ITenancyModuleApi tenancyModuleApi,
        ICurrentUserContext currentUserContext)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _membershipValidator = membershipValidator;
        _tenancyModuleApi = tenancyModuleApi;
        _currentUserContext = currentUserContext;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { code = "Auth.InvalidCredentials", detail = "نام کاربری یا کلمه عبور اشتباه است." });
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { code = "Auth.InvalidCredentials", detail = "نام کاربری یا کلمه عبور اشتباه است." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        // Fallback default or requested Tenant & Company
        var tenantId = request.TenantId ?? Guid.NewGuid();
        var companyId = request.CompanyId ?? Guid.NewGuid();

        var permissions = await _membershipValidator.GetUserCompanyPermissionsAsync(user.Id, tenantId, companyId, cancellationToken);
        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _tokenService.GenerateAccessToken(user, tenantId, companyId, null, permissions, roles);
        var refreshToken = await _tokenService.GenerateAndSaveRefreshTokenAsync(user.Id, tenantId, companyId, cancellationToken);

        SetRefreshTokenCookie(refreshToken);

        return Ok(new LoginResponse(accessToken, user.Email!, user.FullName, tenantId, companyId, permissions));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var rawRefreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Unauthorized(new { code = "Auth.NoRefreshToken", detail = "توکن تمدید نشست یافت نشد." });
        }

        // زمینه از refresh token ذخیره‌شده — نه از JWT (که هنگام refresh موجود نیست)
        var resolved = await _tokenService.ResolveActiveRefreshTokenAsync(rawRefreshToken, cancellationToken);
        if (resolved is null)
        {
            Response.Cookies.Delete("refreshToken");
            return Unauthorized(new { code = "Auth.SessionExpired", detail = "نشست کاری شما منقضی یا نامعتبر شده است." });
        }

        // تعویض شرکت: هدرهای زمینه (X-Tenant-Id / X-Company-Id) در صورت ارسال، عضویت کاربر در آن شرکت را تعیین می‌کنند
        var tenantId = resolved.Value.TenantId;
        var companyId = resolved.Value.CompanyId;
        if (Guid.TryParse(Request.Headers[TenantResolutionMiddleware.TenantHeaderKey], out var headerTenant))
        {
            tenantId = headerTenant;
        }
        if (Guid.TryParse(Request.Headers[TenantResolutionMiddleware.CompanyHeaderKey], out var headerCompany))
        {
            companyId = headerCompany;
        }

        var isMember = await _membershipValidator.HasActiveMembershipAsync(resolved.Value.UserId, tenantId, companyId, cancellationToken);
        if (!isMember)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { code = "Auth.CompanyAccessDenied", detail = "کاربر عضو فعال این شرکت نیست." });
        }

        var user = await _userManager.FindByIdAsync(resolved.Value.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            Response.Cookies.Delete("refreshToken");
            return Unauthorized(new { code = "Auth.SessionExpired", detail = "کاربر غیرفعال است یا وجود ندارد." });
        }

        var permissions = await _membershipValidator.GetUserCompanyPermissionsAsync(resolved.Value.UserId, tenantId, companyId, cancellationToken);
        var roles = await _userManager.GetRolesAsync(user);

        var result = await _tokenService.RotateRefreshTokenAsync(
            rawRefreshToken,
            tenantId,
            companyId,
            null,
            permissions,
            roles,
            cancellationToken);

        if (result is null)
        {
            Response.Cookies.Delete("refreshToken");
            return Unauthorized(new { code = "Auth.SessionExpired", detail = "نشست کاری شما منقضی یا نامعتبر شده است." });
        }

        SetRefreshTokenCookie(result.Value.NewRefreshToken);

        return Ok(new RefreshTokenResponse(result.Value.NewAccessToken, tenantId, companyId, permissions));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var rawRefreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            await _tokenService.RevokeRefreshTokenAsync(rawRefreshToken, cancellationToken);
        }

        Response.Cookies.Delete("refreshToken");
        return Ok(new { message = "خروج با موفقیت انجام شد." });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        return Ok(new
        {
            userId = _currentUserContext.UserId,
            email = _currentUserContext.Email,
            activeTenantId = _currentUserContext.ActiveTenantId,
            activeCompanyId = _currentUserContext.ActiveCompanyId,
            roles = _currentUserContext.Roles,
            permissions = _currentUserContext.Permissions,
        });
    }

    /// <summary>شرکت‌های واقعی کاربر از memberships — مبنای صفحه انتخاب شرکت و Switcher.</summary>
    [HttpGet("companies")]
    [Authorize]
    public async Task<IActionResult> GetUserCompanies(CancellationToken cancellationToken)
    {
        if (_currentUserContext.UserId is not { } userId)
        {
            return Unauthorized(new { code = "Auth.InvalidContext", detail = "زمینه کاربر نامعتبر است." });
        }

        var tenants = await _tenancyModuleApi.GetUserCompaniesAsync(userId, cancellationToken);
        return Ok(tenants);
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}
