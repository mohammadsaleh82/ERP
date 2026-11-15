using ERP.BuildingBlocks.Results;

namespace ERP.BuildingBlocks.Exceptions;

public class AppException : Exception
{
    public string Code { get; }

    public AppException(string code, string message) : base(message)
    {
        Code = code;
    }

    public AppException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}

public sealed class ValidationException : AppException
{
    public IReadOnlyCollection<Error> Errors { get; }

    public ValidationException(IReadOnlyCollection<Error> errors)
        : base("Validation.Error", "One or more validation failures occurred.")
    {
        Errors = errors;
    }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string resourceName, object key)
        : base("Resource.NotFound", $"{resourceName} with key '{key}' was not found.")
    {
    }
}
