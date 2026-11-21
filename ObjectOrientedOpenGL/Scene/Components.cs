using ObjectOrientedOpenGL.Core;
using ObjectOrientedOpenGL.Graphics;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Scene;

public sealed class NameComponent(string name = "Entity", bool active = true)
{
    public string Name { get; set; } = name;
    public bool Active { get; set; } = active;
}

public sealed class TransformComponent
{
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Scale { get; set; } = Vector3.One;

    public Matrix4 LocalMatrix =>
        Matrix4.CreateScale(Scale) *
        Matrix4.CreateFromQuaternion(Rotation) *
        Matrix4.CreateTranslation(Position);

    public Matrix4 WorldMatrix { get; internal set; } = Matrix4.Identity;

    public Vector3 WorldPosition => WorldMatrix.ExtractTranslation();

    public Vector3 EulerAnglesDegrees
    {
        get => Rotation.ToEulerAngles() * (180f / MathF.PI);
        set => Rotation = Quaternion.FromEulerAngles(value * (MathF.PI / 180f));
    }

    public void SetFromLocalMatrix(Matrix4 localMatrix)
    {
        Position = localMatrix.ExtractTranslation();
        Scale = localMatrix.ExtractScale();
        Rotation = localMatrix.ExtractRotation(true);
    }
}

public sealed class HierarchyComponent
{
    public uint? ParentId { get; internal set; }
    public List<uint> Children { get; } = [];
}

public sealed class MeshRendererComponent(Mesh mesh, PbrMaterial material, bool visible = true)
{
    public Mesh Mesh { get; set; } = mesh;
    public PbrMaterial Material { get; set; } = material;
    public bool Visible { get; set; } = visible;
}

public enum LightType
{
    Directional,
    Point
}

public sealed class LightComponent
{
    public LightType Type { get; set; } = LightType.Point;
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 3.0f;
    public float Range { get; set; } = 12.0f;
}

public sealed class RotatorComponent(Vector3 axis, float radiansPerSecond)
{
    public Vector3 Axis { get; set; } = axis;
    public float Speed { get; set; } = radiansPerSecond;
}
