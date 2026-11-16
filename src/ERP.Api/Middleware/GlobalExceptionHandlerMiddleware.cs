using System.Net;
using System.Text.Json;
using ERP.BuildingBlocks.Exceptions;
using ERP.BuildingBlocks.Results;

namespace ERP.Api.Middleware;

public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex, environment.IsDevelopment());
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception,
        bool isDevelopment)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, code, message, errors) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "Validation.Failure",
                validationEx.Message,
                validationEx.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray())
            ),
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                notFoundEx.Code,
                notFoundEx.Message,
                null
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Auth.Unauthorized",
                "دسترسی غیرمجاز. لطفا وارد حساب کاربری خود شوید.",
                null
            ),
            AppException appEx => (
                HttpStatusCode.BadRequest,
                appEx.Code,
                appEx.Message,
                null
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "Server.InternalError",
                "خطای غیرمنتظره‌ای در سرور رخ داده است.",
                null
            )
        };

        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{(int)statusCode}",
            ["title"] = statusCode.ToString(),
            ["status"] = (int)statusCode,
            ["detail"] = message,
            ["code"] = code,
            ["instance"] = context.Request.Path.Value
        };

        if (errors is not null)
        {
            problemDetails["errors"] = errors;
        }

        if (isDevelopment && statusCode == HttpStatusCode.InternalServerError)
        {
            problemDetails["exception"] = exception.Message;
            problemDetails["stackTrace"] = exception.StackTrace;
        }

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }
}
