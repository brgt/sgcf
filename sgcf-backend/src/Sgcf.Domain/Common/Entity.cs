namespace Sgcf.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private set; }
    protected Entity() => Id = Guid.CreateVersion7();
    protected Entity(Guid id) => Id = id;
    public override bool Equals(object? obj) => obj is Entity e && e.GetType() == GetType() && Id == e.Id;
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(Entity? l, Entity? r) => l?.Equals(r) ?? r is null;
    public static bool operator !=(Entity? l, Entity? r) => !(l == r);
}
