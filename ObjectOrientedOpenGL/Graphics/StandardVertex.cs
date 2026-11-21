using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Graphics;

/// <summary>
/// Canonical vertex format shared by procedural primitives and glTF/GLB meshes (supports skeletal skinning).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct StandardVertex(
    Vector3 position,
    Vector3 normal,
    Vector4 tangent,
    Vector2 texCoord,
    Vector4 joints = default,
    Vector4 weights = default)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
    public Vector4 Tangent = tangent;
    public Vector2 TexCoord = texCoord;
    public Vector4 Joints = joints;
    public Vector4 Weights = weights;

    public static Core.VertexBuffer.Attribute[] Attributes =>
    [
        new(0, 3), // Position
        new(1, 3), // Normal
        new(2, 4), // Tangent (xyz + handedness w)
        new(3, 2), // TexCoord
        new(4, 4), // Joints (bone indices as floats)
        new(5, 4), // Weights (bone weights)
    ];
}

/// <summary>
/// Axis-aligned bounding box (AABB).
/// </summary>
public struct BoundingBox(Vector3 min, Vector3 max)
{
    public Vector3 Min = min;
    public Vector3 Max = max;

    public static BoundingBox Empty => new(
        new Vector3(float.PositiveInfinity),
        new Vector3(float.NegativeInfinity));

    public readonly bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;
    public readonly Vector3 Center => IsEmpty ? Vector3.Zero : (Min + Max) * 0.5f;
    public readonly Vector3 Size => IsEmpty ? Vector3.Zero : Max - Min;

    public void Expand(Vector3 point)
    {
        Min = Vector3.ComponentMin(Min, point);
        Max = Vector3.ComponentMax(Max, point);
    }

    public void Expand(BoundingBox other)
    {
        if (other.IsEmpty) return;
        Min = Vector3.ComponentMin(Min, other.Min);
        Max = Vector3.ComponentMax(Max, other.Max);
    }

    public readonly BoundingBox Transform(Matrix4 matrix)
    {
        if (IsEmpty) return Empty;
        var result = Empty;
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            var corner = new Vector4(
                x == 0 ? Min.X : Max.X,
                y == 0 ? Min.Y : Max.Y,
                z == 0 ? Min.Z : Max.Z,
                1.0f);
            result.Expand((corner * matrix).Xyz);
        }
        return result;
    }
}
