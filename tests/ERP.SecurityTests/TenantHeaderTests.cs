using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ERP.SecurityTests;

public class TenantHeaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TenantHeaderTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RequestWithTenantHeader_ShouldBeAcceptedByMiddleware()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-Company-Id", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
