using BepuPhysics;
using BepuPhysics.Collidables;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Physics;

public enum ColliderShape
{
    Box,
    Sphere,
    Capsule
}

public enum BodyMotionType
{
    Static,
    Dynamic,
    Kinematic
}

/// <summary>
/// Collision shape attached to an ECS entity for BepuPhysics2 simulation.
/// </summary>
public sealed class ColliderComponent
{
    public ColliderShape Shape { get; set; } = ColliderShape.Box;
    /// <summary>Full extents (width, height, depth) when <see cref="Shape"/> is <see cref="ColliderShape.Box"/>.</summary>
    public Vector3 BoxSize { get; set; } = Vector3.One;
    /// <summary>Radius when <see cref="Shape"/> is <see cref="ColliderShape.Sphere"/> or <see cref="ColliderShape.Capsule"/>.</summary>
    public float Radius { get; set; } = 0.5f;
    /// <summary>Cylinder length (excluding hemispherical caps) when <see cref="Shape"/> is <see cref="ColliderShape.Capsule"/>.</summary>
    public float Length { get; set; } = 1.0f;

    public static ColliderComponent Box(Vector3 fullSize) => new() { Shape = ColliderShape.Box, BoxSize = fullSize };
    public static ColliderComponent Box(float width, float height, float depth) => Box(new Vector3(width, height, depth));
    public static ColliderComponent Sphere(float radius) => new() { Shape = ColliderShape.Sphere, Radius = radius };
    public static ColliderComponent Capsule(float radius, float length) => new() { Shape = ColliderShape.Capsule, Radius = radius, Length = length };
}

/// <summary>
/// Rigid body properties for an ECS entity simulated by <see cref="PhysicsWorld"/> (BepuPhysics2).
/// </summary>
public sealed class RigidBodyComponent
{
    public BodyMotionType MotionType { get; set; } = BodyMotionType.Dynamic;
    public float Mass { get; set; } = 1.0f;
    public Vector3 LinearVelocity { get; set; }
    public Vector3 AngularVelocity { get; set; }
    public bool IsAwake { get; internal set; } = true;

    internal BodyHandle? BodyHandle { get; set; }
    internal StaticHandle? StaticHandle { get; set; }
    internal TypedIndex? ShapeIndex { get; set; }
    internal Vector3 PendingImpulse { get; set; }
    internal bool VelocityDirty { get; set; }

    public void ApplyImpulse(Vector3 impulse)
    {
        PendingImpulse += impulse;
    }

    public void SetLinearVelocity(Vector3 velocity)
    {
        LinearVelocity = velocity;
        VelocityDirty = true;
    }

    public static RigidBodyComponent Static() => new() { MotionType = BodyMotionType.Static, Mass = 0f };
    public static RigidBodyComponent Dynamic(float mass = 1.0f) => new() { MotionType = BodyMotionType.Dynamic, Mass = mass };
    public static RigidBodyComponent Kinematic() => new() { MotionType = BodyMotionType.Kinematic, Mass = 0f };
}
