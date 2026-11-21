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
    public bool CastShadows { get; set; } = true;
    public AnimatorComponent? Animator { get; set; }
    public int SkinIndex { get; set; } = -1;
}

/// <summary>
/// Plays glTF 2.0 node-transform and skeletal-skinning animations on an ECS hierarchy spawned from a <see cref="Model"/>.
/// </summary>
public sealed class AnimatorComponent
{
    public const int MaxJoints = 128;

    public IReadOnlyList<AnimationClip> Clips { get; }
    public IReadOnlyList<SkinData> Skins { get; }
    public Dictionary<int, Entity> NodeEntities { get; }
    public int ActiveClipIndex { get; set; }
    public float CurrentTime { get; set; }
    public float Speed { get; set; } = 1.0f;
    public bool Playing { get; set; } = true;
    public bool Loop { get; set; } = true;

    private readonly Matrix4[][] _skinPalettes;

    public AnimatorComponent(
        IReadOnlyList<AnimationClip> clips,
        IReadOnlyList<SkinData> skins,
        Dictionary<int, Entity> nodeEntities)
    {
        Clips = clips;
        Skins = skins;
        NodeEntities = nodeEntities;
        _skinPalettes = new Matrix4[skins.Count][];
        for (int s = 0; s < skins.Count; s++)
        {
            int n = Math.Min(skins[s].JointNodeIndices.Length, MaxJoints);
            _skinPalettes[s] = new Matrix4[n];
            Array.Fill(_skinPalettes[s], Matrix4.Identity);
        }
    }

    public AnimationClip? ActiveClip =>
        ActiveClipIndex >= 0 && ActiveClipIndex < Clips.Count ? Clips[ActiveClipIndex] : null;

    public ReadOnlySpan<Matrix4> GetBonePalette(int skinIndex, Matrix4 meshWorldMatrix)
    {
        if (skinIndex < 0 || skinIndex >= Skins.Count)
            return default;

        var skin = Skins[skinIndex];
        var palette = _skinPalettes[skinIndex];
        var invMeshWorld = meshWorldMatrix.Inverted();

        for (int i = 0; i < palette.Length; i++)
        {
            int jointNodeIdx = skin.JointNodeIndices[i];
            if (NodeEntities.TryGetValue(jointNodeIdx, out var jointEntity) && jointEntity.IsValid)
            {
                var jointWorld = jointEntity.Get<TransformComponent>().WorldMatrix;
                // Row-major OpenTK: v_mesh * IBM * JointWorld * InvMeshWorld
                palette[i] = skin.InverseBindMatrices[i] * jointWorld * invMeshWorld;
            }
            else
            {
                palette[i] = Matrix4.Identity;
            }
        }

        return palette;
    }
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
