namespace ERP.Api.Middleware;

/// <summary>
/// هدرهای امنیتی استاندارد روی همه پاسخ‌ها — جلوگیری از MIME-sniffing،
/// embedding، نشت Referrer و دسترسی iframe/اسکریپت خارجی.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            return Task.CompletedTask;
        });

        await next(context);
    }
}
