using System.IO;
using System.Net;
using System.Text.Json;
using ERP.Api.Middleware;
using ERP.BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.IntegrationTests;

public class ProblemDetailsEndpointTests
{
    private sealed class MockEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "ERP.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task WhenNotFoundExceptionThrown_ShouldGenerateRfc7807ProblemDetails()
    {
        var middleware = new GlobalExceptionHandlerMiddleware(
            next: (ctx) => throw new NotFoundException("Company", Guid.NewGuid()),
            logger: NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            environment: new MockEnvironment()
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/companies/00000000-0000-0000-0000-000000000001";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        context.Response.ContentType.Should().Contain("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("type").GetString().Should().Be("https://httpstatuses.com/404");
        root.GetProperty("title").GetString().Should().Be("NotFound");
        root.GetProperty("code").GetString().Should().Be("Resource.NotFound");
        root.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("instance").GetString().Should().Be("/api/v1/companies/00000000-0000-0000-0000-000000000001");
    }

    [Fact]
    public async Task WhenUnexpectedExceptionThrownInProduction_ShouldNotLeakStackTrace()
    {
        var middleware = new GlobalExceptionHandlerMiddleware(
            next: (ctx) => throw new InvalidOperationException("Internal secret database connection leak!"),
            logger: NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            environment: new MockEnvironment { EnvironmentName = Environments.Production }
        );

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/internal-action";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        context.Response.ContentType.Should().Contain("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(500);
        root.GetProperty("code").GetString().Should().Be("Server.InternalError");
        // Ensure stackTrace and internal exception message are hidden in production
        root.TryGetProperty("stackTrace", out _).Should().BeFalse();
        root.TryGetProperty("exception", out _).Should().BeFalse();
    }
}
