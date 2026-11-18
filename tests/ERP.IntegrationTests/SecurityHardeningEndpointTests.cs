using Microsoft.AspNetCore.Mvc.Testing;
using ERP.Api;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// هدرهای امنیتی و پاک‌سازی استثناها روی پاسخ‌های API واقعی (PROMPT 09 —
/// امنیت عملیاتی).
/// </summary>
public class SecurityHeadersEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityHeadersEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AllResponses_IncludeSecurityHeaders()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        response.Headers.Should().NotBeNull();
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.Contains("X-Frame-Options").Should().BeTrue();
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.Contains("Referrer-Policy").Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task ErrorResponses_AreProblemDetails_WithoutStackTraces()
    {
        var client = _factory.CreateClient();

        // درخواست بدون هدرهای زمینه → خطای کنترل‌شده
        var response = await client.GetAsync("/api/v1/accounting/accounts");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();

        if (!string.IsNullOrEmpty(body))
        {
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // هیچ Stack Trace یا پیام داخلی در پاسخ نباید باشد
            root.EnumerateObject().Select(p => p.Name).Should().NotContain("stackTrace");
            root.EnumerateObject().Select(p => p.Name).Should().NotContain("exception");
            body.Should().NotContain("at ERP.");
            body.Should().NotContain("Exception:");
        }
        else
        {
            // پاسخ خالی 401 از middleware احراز هویت: هیچ نشت پیام داخلی
            body.Should().NotContain("at ERP.");
        }
    }
}

/// <summary>
/// محدودسازی نرخ درخواست — درخواست انبوه از یک IP باید 429 بگیرد.
/// (سرور مستقل: RateLimiter در حافظه هر نمونه سرور جدا است.)
/// </summary>
public class RateLimitingEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BurstRequests_FromSameIp_EventuallyReturn429()
    {
        var client = _factory.CreateClient();
        var saw429 = false;

        for (var i = 0; i < 130; i++)
        {
            var response = await client.GetAsync("/health");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                saw429 = true;
                break;
            }
        }

        saw429.Should().BeTrue("محدودسازی نرخ باید بعد از ۱۰۰ درخواست در پنجره فعال شود");
    }
}
