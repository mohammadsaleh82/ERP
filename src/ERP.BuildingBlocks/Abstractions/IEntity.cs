namespace ERP.BuildingBlocks.Abstractions;

public interface IEntity<out TId>
{
    TId Id { get; }
}

public interface IEntity : IEntity<Guid>;
