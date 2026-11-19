using ERP.BuildingBlocks.Security;

namespace ERP.Api.Middleware;

/// <summary>تامین شناسه همبستگی از HttpContext (هدر X-Correlation-Id یا TraceIdentifier).</summary>
public sealed class HttpCorrelationIdProvider(IHttpContextAccessor httpContextAccessor) : ICorrelationIdProvider
{
    public const string CorrelationIdItemKey = "CorrelationId";

    public string GetCorrelationId()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
            return Guid.NewGuid().ToString("N");

        return context.Items.TryGetValue(CorrelationIdItemKey, out var value) &&
               value is string correlationId && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId
            : context.TraceIdentifier;
    }
}
