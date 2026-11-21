namespace ObjectOrientedOpenGL.Scene;

/// <summary>
/// Lightweight handle referencing an entity inside a <see cref="World"/>.
/// </summary>
public readonly struct Entity(uint id, World world) : IEquatable<Entity>
{
    public uint Id { get; } = id;
    public World World { get; } = world;

    public bool IsValid => World is not null && World.IsAlive(Id);

    public Entity Add<T>(T component) where T : class
    {
        World.Add(Id, component);
        return this;
    }

    public T Get<T>() where T : class => World.Get<T>(Id);

    public bool TryGet<T>(out T? component) where T : class => World.TryGet(Id, out component);

    public bool Has<T>() where T : class => World.Has<T>(Id);

    public bool Remove<T>() where T : class => World.Remove<T>(Id);

    public Entity SetParent(Entity? parent)
    {
        World.SetParent(Id, parent?.Id);
        return this;
    }

    public void Destroy() => World.DestroyEntity(Id);

    public bool Equals(Entity other) => Id == other.Id && ReferenceEquals(World, other.World);
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Id, World);
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
    public override string ToString() => TryGet<NameComponent>(out var n) && n != null ? $"{n.Name} (#{Id})" : $"Entity #{Id}";
}
