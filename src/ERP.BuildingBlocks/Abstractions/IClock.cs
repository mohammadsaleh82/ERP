namespace ERP.BuildingBlocks.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
