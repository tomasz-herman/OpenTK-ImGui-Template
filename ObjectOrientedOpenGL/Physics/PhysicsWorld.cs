using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using ObjectOrientedOpenGL.Scene;
using OpenTK.Mathematics;
using Quaternion = OpenTK.Mathematics.Quaternion;
using SysQuat = System.Numerics.Quaternion;
using SysVec3 = System.Numerics.Vector3;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace ObjectOrientedOpenGL.Physics;

public readonly record struct RaycastHit(Entity Entity, Vector3 Point, Vector3 Normal, float Distance);

/// <summary>
/// BepuPhysics2 simulation integrated as an ECS <see cref="ISystem"/>.
/// Automatically manages dynamic, kinematic, and static bodies for entities carrying
/// <see cref="TransformComponent"/>, <see cref="RigidBodyComponent"/>, and <see cref="ColliderComponent"/>.
/// </summary>
public sealed class PhysicsWorld : ISystem, IDisposable
{
    private readonly BufferPool _bufferPool;
    private readonly ThreadDispatcher? _threadDispatcher;
    private readonly Dictionary<uint, RegisteredBody> _registered = new();
    private readonly Dictionary<int, uint> _bodyHandleToEntity = new();
    private readonly Dictionary<int, uint> _staticHandleToEntity = new();

    private float _accumulator;
    private bool _disposed;

    public Simulation Simulation { get; }

    public Vector3 Gravity
    {
        get
        {
            if (Simulation.PoseIntegrator is PoseIntegrator<EnginePoseIntegratorCallbacks> pi)
            {
                var g = pi.Callbacks.Gravity;
                return new Vector3(g.X, g.Y, g.Z);
            }
            return new Vector3(0f, -9.81f, 0f);
        }
        set
        {
            if (Simulation.PoseIntegrator is PoseIntegrator<EnginePoseIntegratorCallbacks> pi)
                pi.Callbacks.Gravity = new SysVec3(value.X, value.Y, value.Z);
        }
    }

    public float FixedTimeStep { get; set; } = 1.0f / 60.0f;
    public int MaxSubSteps { get; set; } = 4;
    public bool Paused { get; set; }
    public int BodyCount => _bodyHandleToEntity.Count;
    public int StaticCount => _staticHandleToEntity.Count;

    public PhysicsWorld(Vector3? gravity = null, int? threadCount = null)
    {
        var g = gravity ?? new Vector3(0f, -9.81f, 0f);
        _bufferPool = new BufferPool();
        int workers = threadCount ?? Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
        if (workers > 1)
            _threadDispatcher = new ThreadDispatcher(workers);

        Simulation = Simulation.Create(
            _bufferPool,
            new EngineNarrowPhaseCallbacks(),
            new EnginePoseIntegratorCallbacks(new SysVec3(g.X, g.Y, g.Z)),
            new SolveDescription(8, 1));
    }

    public void Update(World world, float deltaTime)
    {
        if (_disposed) return;

        ReconcileEntities(world);

        if (Paused || deltaTime <= 0f)
            return;

        // Push kinematic transforms & pending impulses/velocities into Bepu
        foreach (var (entity, transform, rb, _) in world.Query<TransformComponent, RigidBodyComponent, ColliderComponent>())
        {
            if (!rb.BodyHandle.HasValue) continue;
            var bodyRef = Simulation.Bodies.GetBodyReference(rb.BodyHandle.Value);

            if (rb.MotionType == BodyMotionType.Kinematic)
            {
                bodyRef.Pose = ToRigidPose(transform.Position, transform.Rotation);
                bodyRef.Awake = true;
            }
            else if (rb.MotionType == BodyMotionType.Dynamic)
            {
                if (rb.VelocityDirty)
                {
                    bodyRef.Velocity.Linear = ToSys(rb.LinearVelocity);
                    bodyRef.Velocity.Angular = ToSys(rb.AngularVelocity);
                    bodyRef.Awake = true;
                    rb.VelocityDirty = false;
                }
                if (rb.PendingImpulse.LengthSquared > 1e-8f)
                {
                    bodyRef.ApplyLinearImpulse(ToSys(rb.PendingImpulse));
                    bodyRef.Awake = true;
                    rb.PendingImpulse = Vector3.Zero;
                }
            }
        }

        // Fixed-timestep accumulation
        _accumulator += MathF.Min(deltaTime, FixedTimeStep * MaxSubSteps);
        int steps = 0;
        while (_accumulator >= FixedTimeStep && steps < MaxSubSteps)
        {
            Simulation.Timestep(FixedTimeStep, _threadDispatcher);
            _accumulator -= FixedTimeStep;
            steps++;
        }

        // Pull dynamic poses back into ECS TransformComponent
        foreach (var (entity, transform, rb) in world.Query<TransformComponent, RigidBodyComponent>())
        {
            if (rb.MotionType != BodyMotionType.Dynamic || !rb.BodyHandle.HasValue)
                continue;

            var bodyRef = Simulation.Bodies.GetBodyReference(rb.BodyHandle.Value);
            transform.Position = ToTk(bodyRef.Pose.Position);
            transform.Rotation = ToTk(bodyRef.Pose.Orientation);
            rb.LinearVelocity = ToTk(bodyRef.Velocity.Linear);
            rb.AngularVelocity = ToTk(bodyRef.Velocity.Angular);
            rb.IsAwake = bodyRef.Awake;
        }
    }

    /// <summary>
    /// Teleports an entity's physics body or static to match its current <see cref="TransformComponent"/>.
    /// Call this after manual edits (e.g. from ImGuizmo or Inspector).
    /// </summary>
    public void SyncEntityToPhysics(Entity entity)
    {
        if (_disposed || !entity.IsValid) return;
        if (!entity.TryGet<TransformComponent>(out var tr) || tr is null) return;
        if (!entity.TryGet<RigidBodyComponent>(out var rb) || rb is null) return;

        var pose = ToRigidPose(tr.Position, tr.Rotation);
        if (rb.BodyHandle.HasValue && Simulation.Bodies.BodyExists(rb.BodyHandle.Value))
        {
            var body = Simulation.Bodies.GetBodyReference(rb.BodyHandle.Value);
            body.Pose = pose;
            body.Velocity = default;
            body.Awake = true;
        }
        else if (rb.StaticHandle.HasValue && Simulation.Statics.StaticExists(rb.StaticHandle.Value))
        {
            var stat = Simulation.Statics.GetStaticReference(rb.StaticHandle.Value);
            stat.Pose = pose;
            stat.UpdateBounds();
        }
    }

    /// <summary>
    /// Removes all registered bodies and statics from the simulation.
    /// </summary>
    public void Reset()
    {
        if (_disposed) return;

        foreach (var reg in _registered.Values)
            RemoveFromSimulation(reg);

        _registered.Clear();
        _bodyHandleToEntity.Clear();
        _staticHandleToEntity.Clear();
        _accumulator = 0f;
    }

    /// <summary>
    /// Casts a ray against all physics colliders in the simulation.
    /// </summary>
    public bool Raycast(World world, Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
    {
        var handler = new ClosestRayHitHandler();
        var dir = direction.Normalized();
        Simulation.RayCast(ToSys(origin), ToSys(dir), maxDistance, ref handler);

        if (handler.Hit)
        {
            uint entityId = 0;
            bool found = handler.Collidable.Mobility == CollidableMobility.Static
                ? _staticHandleToEntity.TryGetValue(handler.Collidable.StaticHandle.Value, out entityId)
                : _bodyHandleToEntity.TryGetValue(handler.Collidable.BodyHandle.Value, out entityId);

            if (found && world.IsAlive(entityId))
            {
                var pt = origin + dir * handler.T;
                hit = new RaycastHit(new Entity(entityId, world), pt, ToTk(handler.Normal), handler.T);
                return true;
            }
        }

        hit = default;
        return false;
    }

    private void ReconcileEntities(World world)
    {
        // 1. Remove destroyed entities or entities that lost physics components
        if (_registered.Count > 0)
        {
            List<uint>? toRemove = null;
            foreach (var kv in _registered)
            {
                uint id = kv.Key;
                if (!world.IsAlive(id) ||
                    !world.Has<RigidBodyComponent>(id) ||
                    !world.Has<ColliderComponent>(id))
                {
                    (toRemove ??= []).Add(id);
                }
            }

            if (toRemove != null)
            {
                foreach (uint id in toRemove)
                {
                    var reg = _registered[id];
                    RemoveFromSimulation(reg);
                    _registered.Remove(id);
                }
            }
        }

        // 2. Register newly added physics entities
        foreach (var (entity, transform, rb, collider) in world.Query<TransformComponent, RigidBodyComponent, ColliderComponent>())
        {
            if (_registered.ContainsKey(entity.Id))
                continue;

            RegisterEntity(entity.Id, transform, rb, collider);
        }
    }

    private void RegisterEntity(uint entityId, TransformComponent transform, RigidBodyComponent rb, ColliderComponent collider)
    {
        var pose = ToRigidPose(transform.Position, transform.Rotation);
        TypedIndex shapeIndex;
        BodyInertia inertia = default;

        switch (collider.Shape)
        {
            case ColliderShape.Sphere:
                var sphere = new Sphere(MathF.Max(collider.Radius, 0.01f));
                shapeIndex = Simulation.Shapes.Add(sphere);
                if (rb.MotionType == BodyMotionType.Dynamic)
                    inertia = sphere.ComputeInertia(MathF.Max(rb.Mass, 0.001f));
                break;

            case ColliderShape.Capsule:
                var capsule = new Capsule(MathF.Max(collider.Radius, 0.01f), MathF.Max(collider.Length, 0.01f));
                shapeIndex = Simulation.Shapes.Add(capsule);
                if (rb.MotionType == BodyMotionType.Dynamic)
                    inertia = capsule.ComputeInertia(MathF.Max(rb.Mass, 0.001f));
                break;

            default:
                var box = new Box(
                    MathF.Max(collider.BoxSize.X, 0.01f),
                    MathF.Max(collider.BoxSize.Y, 0.01f),
                    MathF.Max(collider.BoxSize.Z, 0.01f));
                shapeIndex = Simulation.Shapes.Add(box);
                if (rb.MotionType == BodyMotionType.Dynamic)
                    inertia = box.ComputeInertia(MathF.Max(rb.Mass, 0.001f));
                break;
        }

        rb.ShapeIndex = shapeIndex;

        if (rb.MotionType == BodyMotionType.Static)
        {
            var handle = Simulation.Statics.Add(new StaticDescription(pose, shapeIndex));
            rb.StaticHandle = handle;
            rb.BodyHandle = null;
            _staticHandleToEntity[handle.Value] = entityId;
            _registered[entityId] = new RegisteredBody(null, handle, shapeIndex, rb);
        }
        else if (rb.MotionType == BodyMotionType.Kinematic)
        {
            var desc = BodyDescription.CreateKinematic(pose, new CollidableDescription(shapeIndex, 0.1f), new BodyActivityDescription(0.01f));
            var handle = Simulation.Bodies.Add(desc);
            rb.BodyHandle = handle;
            rb.StaticHandle = null;
            _bodyHandleToEntity[handle.Value] = entityId;
            _registered[entityId] = new RegisteredBody(handle, null, shapeIndex, rb);
        }
        else
        {
            var vel = new BodyVelocity(ToSys(rb.LinearVelocity), ToSys(rb.AngularVelocity));
            var desc = BodyDescription.CreateDynamic(pose, vel, inertia, new CollidableDescription(shapeIndex, 0.1f), new BodyActivityDescription(0.01f));
            var handle = Simulation.Bodies.Add(desc);
            rb.BodyHandle = handle;
            rb.StaticHandle = null;
            _bodyHandleToEntity[handle.Value] = entityId;
            _registered[entityId] = new RegisteredBody(handle, null, shapeIndex, rb);
        }
    }

    private void RemoveFromSimulation(RegisteredBody reg)
    {
        if (reg.BodyHandle.HasValue)
        {
            _bodyHandleToEntity.Remove(reg.BodyHandle.Value.Value);
            if (Simulation.Bodies.BodyExists(reg.BodyHandle.Value))
                Simulation.Bodies.Remove(reg.BodyHandle.Value);
            reg.RigidBody.BodyHandle = null;
        }

        if (reg.StaticHandle.HasValue)
        {
            _staticHandleToEntity.Remove(reg.StaticHandle.Value.Value);
            if (Simulation.Statics.StaticExists(reg.StaticHandle.Value))
                Simulation.Statics.Remove(reg.StaticHandle.Value);
            reg.RigidBody.StaticHandle = null;
        }

        if (reg.ShapeIndex.Exists)
        {
            Simulation.Shapes.Remove(reg.ShapeIndex);
            reg.RigidBody.ShapeIndex = null;
        }
    }

    private static RigidPose ToRigidPose(Vector3 pos, Quaternion rot) =>
        new(ToSys(pos), ToSys(rot));

    private static SysVec3 ToSys(Vector3 v) => new(v.X, v.Y, v.Z);
    private static SysQuat ToSys(Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    private static Vector3 ToTk(SysVec3 v) => new(v.X, v.Y, v.Z);
    private static Quaternion ToTk(SysQuat q) => new(q.X, q.Y, q.Z, q.W);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Simulation.Dispose();
        _threadDispatcher?.Dispose();
        _bufferPool.Clear();
    }

    private readonly record struct RegisteredBody(
        BodyHandle? BodyHandle,
        StaticHandle? StaticHandle,
        TypedIndex ShapeIndex,
        RigidBodyComponent RigidBody);

    private struct ClosestRayHitHandler : IRayHitHandler
    {
        public bool Hit;
        public float T;
        public SysVec3 Normal;
        public CollidableReference Collidable;

        public bool AllowTest(CollidableReference collidable) => true;
        public bool AllowTest(CollidableReference collidable, int childIndex) => true;

        public void OnRayHit(in RayData ray, ref float maximumT, float t, in SysVec3 normal, CollidableReference collidable, int childIndex)
        {
            if (t < maximumT)
            {
                maximumT = t;
                Hit = true;
                T = t;
                Normal = normal;
                Collidable = collidable;
            }
        }
    }
}
