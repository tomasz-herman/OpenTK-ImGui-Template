using ObjectOrientedOpenGL.Graphics;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Scene;

public interface ISystem
{
    void Update(World world, float deltaTime);
}

/// <summary>
/// Lightweight ECS + Scene Graph container.
/// </summary>
public sealed class World
{
    private uint _nextId = 1;
    private readonly HashSet<uint> _alive = [];
    private readonly List<uint> _entityOrder = [];
    private readonly Dictionary<Type, Dictionary<uint, object>> _stores = new();
    private readonly List<ISystem> _systems = [];

    public TransformSystem TransformSystem { get; } = new();

    public World()
    {
        _systems.Add(new RotatorSystem());
        _systems.Add(TransformSystem);
    }

    public IEnumerable<Entity> Entities => _entityOrder.Select(id => new Entity(id, this));

    public int EntityCount => _alive.Count;

    public bool IsAlive(uint id) => _alive.Contains(id);

    public Entity CreateEntity(string name = "Entity", Entity? parent = null)
    {
        uint id = _nextId++;
        _alive.Add(id);
        _entityOrder.Add(id);

        var entity = new Entity(id, this);
        entity.Add(new NameComponent(name));
        entity.Add(new TransformComponent());
        entity.Add(new HierarchyComponent());

        if (parent.HasValue)
            SetParent(id, parent.Value.Id);

        return entity;
    }

    public void DestroyEntity(uint id)
    {
        if (!_alive.Contains(id))
            return;

        // Recursively destroy children first
        if (TryGet<HierarchyComponent>(id, out var hierarchy) && hierarchy != null)
        {
            foreach (uint childId in hierarchy.Children.ToArray())
                DestroyEntity(childId);

            if (hierarchy.ParentId.HasValue && TryGet<HierarchyComponent>(hierarchy.ParentId.Value, out var parentH) && parentH != null)
                parentH.Children.Remove(id);
        }

        _alive.Remove(id);
        _entityOrder.Remove(id);
        foreach (var store in _stores.Values)
            store.Remove(id);
    }

    public void Clear()
    {
        _alive.Clear();
        _entityOrder.Clear();
        _stores.Clear();
        _nextId = 1;
    }

    public void SetParent(uint childId, uint? newParentId)
    {
        if (!_alive.Contains(childId)) return;
        if (newParentId.HasValue && (!_alive.Contains(newParentId.Value) || newParentId.Value == childId)) return;

        var childH = Get<HierarchyComponent>(childId);
        if (childH.ParentId == newParentId) return;

        // Prevent cycles
        uint? cursor = newParentId;
        while (cursor.HasValue)
        {
            if (cursor.Value == childId) return;
            cursor = TryGet<HierarchyComponent>(cursor.Value, out var ch) ? ch?.ParentId : null;
        }

        if (childH.ParentId.HasValue && TryGet<HierarchyComponent>(childH.ParentId.Value, out var oldParentH) && oldParentH != null)
            oldParentH.Children.Remove(childId);

        childH.ParentId = newParentId;
        if (newParentId.HasValue)
            Get<HierarchyComponent>(newParentId.Value).Children.Add(childId);
    }

    public IEnumerable<Entity> GetRootEntities()
    {
        foreach (uint id in _entityOrder)
        {
            if (TryGet<HierarchyComponent>(id, out var h) && h != null && !h.ParentId.HasValue)
                yield return new Entity(id, this);
        }
    }

    public void Add<T>(uint entityId, T component) where T : class
    {
        if (!_alive.Contains(entityId))
            throw new InvalidOperationException($"Entity {entityId} is not alive.");

        if (!_stores.TryGetValue(typeof(T), out var store))
        {
            store = new Dictionary<uint, object>();
            _stores[typeof(T)] = store;
        }
        store[entityId] = component;
    }

    public T Get<T>(uint entityId) where T : class
    {
        if (_stores.TryGetValue(typeof(T), out var store) && store.TryGetValue(entityId, out var comp))
            return (T)comp;

        throw new KeyNotFoundException($"Entity {entityId} does not have component {typeof(T).Name}.");
    }

    public bool TryGet<T>(uint entityId, out T? component) where T : class
    {
        if (_stores.TryGetValue(typeof(T), out var store) && store.TryGetValue(entityId, out var raw))
        {
            component = (T)raw;
            return true;
        }
        component = null;
        return false;
    }

    public bool Has<T>(uint entityId) where T : class =>
        _stores.TryGetValue(typeof(T), out var store) && store.ContainsKey(entityId);

    public bool Remove<T>(uint entityId) where T : class =>
        _stores.TryGetValue(typeof(T), out var store) && store.Remove(entityId);

    public IEnumerable<(Entity Entity, T1 C1)> Query<T1>()
        where T1 : class
    {
        if (!_stores.TryGetValue(typeof(T1), out var s1))
            yield break;

        foreach (var kv in s1)
            yield return (new Entity(kv.Key, this), (T1)kv.Value);
    }

    public IEnumerable<(Entity Entity, T1 C1, T2 C2)> Query<T1, T2>()
        where T1 : class
        where T2 : class
    {
        if (!_stores.TryGetValue(typeof(T1), out var s1) || !_stores.TryGetValue(typeof(T2), out var s2))
            yield break;

        foreach (var kv in s1)
        {
            if (s2.TryGetValue(kv.Key, out var c2))
                yield return (new Entity(kv.Key, this), (T1)kv.Value, (T2)c2);
        }
    }

    public IEnumerable<(Entity Entity, T1 C1, T2 C2, T3 C3)> Query<T1, T2, T3>()
        where T1 : class
        where T2 : class
        where T3 : class
    {
        if (!_stores.TryGetValue(typeof(T1), out var s1) ||
            !_stores.TryGetValue(typeof(T2), out var s2) ||
            !_stores.TryGetValue(typeof(T3), out var s3))
            yield break;

        foreach (var kv in s1)
        {
            if (s2.TryGetValue(kv.Key, out var c2) && s3.TryGetValue(kv.Key, out var c3))
                yield return (new Entity(kv.Key, this), (T1)kv.Value, (T2)c2, (T3)c3);
        }
    }

    public void AddSystem(ISystem system) => _systems.Insert(Math.Max(0, _systems.Count - 1), system);

    public void Update(float deltaTime)
    {
        foreach (var system in _systems)
            system.Update(this, deltaTime);
    }

    /// <summary>
    /// Spawns a loaded glTF <see cref="Model"/> hierarchy into the ECS Scene Graph.
    /// </summary>
    public Entity SpawnModel(Model model, string? rootName = null, Entity? parent = null)
    {
        Entity SpawnNode(ModelNode node, Entity? parentEntity, string? overrideName = null)
        {
            var e = CreateEntity(overrideName ?? node.Name, parentEntity);
            e.Get<TransformComponent>().SetFromLocalMatrix(node.LocalTransform);

            if (node.Primitives.Count == 1)
            {
                var p = node.Primitives[0];
                e.Add(new MeshRendererComponent(p.Mesh, p.Material));
            }
            else if (node.Primitives.Count > 1)
            {
                foreach (var p in node.Primitives)
                {
                    var sub = CreateEntity(p.Name, e);
                    sub.Add(new MeshRendererComponent(p.Mesh, p.Material));
                }
            }

            foreach (var child in node.Children)
                SpawnNode(child, e);

            return e;
        }

        return SpawnNode(model.Root, parent, rootName ?? Path.GetFileNameWithoutExtension(model.Path));
    }
}

/// <summary>
/// Propagates local transforms down the scene graph hierarchy into world matrices.
/// </summary>
public sealed class TransformSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        foreach (var root in world.GetRootEntities())
            UpdateRecursive(world, root.Id, Matrix4.Identity);
    }

    private static void UpdateRecursive(World world, uint entityId, Matrix4 parentWorld)
    {
        var transform = world.Get<TransformComponent>(entityId);
        transform.WorldMatrix = transform.LocalMatrix * parentWorld;

        if (world.TryGet<HierarchyComponent>(entityId, out var hierarchy) && hierarchy != null)
        {
            foreach (uint childId in hierarchy.Children)
                UpdateRecursive(world, childId, transform.WorldMatrix);
        }
    }
}

/// <summary>
/// Rotates any entity carrying a <see cref="RotatorComponent"/>.
/// </summary>
public sealed class RotatorSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        foreach (var (entity, transform, rotator) in world.Query<TransformComponent, RotatorComponent>())
        {
            if (entity.TryGet<NameComponent>(out var name) && name is { Active: false })
                continue;

            var delta = Quaternion.FromAxisAngle(rotator.Axis.Normalized(), rotator.Speed * deltaTime);
            transform.Rotation = (delta * transform.Rotation).Normalized();
        }
    }
}
