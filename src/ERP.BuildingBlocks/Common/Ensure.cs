using System.Diagnostics.CodeAnalysis;

namespace ERP.BuildingBlocks.Common;

public static class Ensure
{
    public static void NotNull<T>([NotNull] T? value, string paramName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    public static void NotNullOrWhiteSpace([NotNull] string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
        }
    }

    public static void GreaterThanZero(decimal value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Value must be greater than zero.");
        }
    }
}
