namespace ERP.BuildingBlocks.Abstractions;

public interface IAggregateRoot<out TId> : IEntity<TId>;

public interface IAggregateRoot : IAggregateRoot<Guid>, IEntity;
