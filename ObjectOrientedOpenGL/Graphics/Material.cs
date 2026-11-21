using ObjectOrientedOpenGL.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Graphics;

public enum AlphaMode
{
    Opaque = 0,
    Mask = 1,
    Blend = 2
}

/// <summary>
/// Base class for all renderable materials.
/// </summary>
public abstract class Material(string name = "Material")
{
    public string Name { get; set; } = name;
    public bool DoubleSided { get; set; }
    public AlphaMode AlphaMode { get; set; } = AlphaMode.Opaque;
    public float AlphaCutoff { get; set; } = 0.5f;

    public abstract void Bind(Shader shader);
}

/// <summary>
/// glTF 2.0 compatible metallic-roughness PBR material.
/// </summary>
public class PbrMaterial(string name = "PbrMaterial") : Material(name)
{
    public Vector4 BaseColorFactor { get; set; } = Vector4.One;
    public float MetallicFactor { get; set; } = 0.0f;
    public float RoughnessFactor { get; set; } = 0.5f;
    public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;

    public Texture? BaseColorTexture { get; set; }
    public Texture? MetallicRoughnessTexture { get; set; }
    public Texture? NormalTexture { get; set; }
    public Texture? OcclusionTexture { get; set; }
    public Texture? EmissiveTexture { get; set; }

    private static Texture? _whiteFallback;
    private static Texture? _flatNormalFallback;

    private static Texture WhiteFallback => _whiteFallback ??= Texture.CreateSolidColor(255, 255, 255, 255);
    private static Texture FlatNormalFallback => _flatNormalFallback ??= Texture.CreateSolidColor(128, 128, 255, 255);

    public override void Bind(Shader shader)
    {
        if (DoubleSided) GL.Disable(EnableCap.CullFace);
        else GL.Enable(EnableCap.CullFace);

        if (AlphaMode == AlphaMode.Blend)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        else
        {
            GL.Disable(EnableCap.Blend);
        }

        shader.LoadFloat4("uBaseColorFactor", BaseColorFactor);
        shader.LoadFloat("uMetallicFactor", MetallicFactor);
        shader.LoadFloat("uRoughnessFactor", RoughnessFactor);
        shader.LoadFloat3("uEmissiveFactor", EmissiveFactor);
        shader.LoadFloat("uAlphaCutoff", AlphaCutoff);
        shader.LoadInteger("uAlphaMode", (int)AlphaMode);
        shader.LoadInteger("uHasNormalMap", NormalTexture != null ? 1 : 0);

        (BaseColorTexture ?? WhiteFallback).ActivateUnit(0);
        shader.LoadInteger("uBaseColorMap", 0);

        (MetallicRoughnessTexture ?? WhiteFallback).ActivateUnit(1);
        shader.LoadInteger("uMetallicRoughnessMap", 1);

        (NormalTexture ?? FlatNormalFallback).ActivateUnit(2);
        shader.LoadInteger("uNormalMap", 2);

        (OcclusionTexture ?? WhiteFallback).ActivateUnit(3);
        shader.LoadInteger("uOcclusionMap", 3);

        (EmissiveTexture ?? WhiteFallback).ActivateUnit(4);
        shader.LoadInteger("uEmissiveMap", 4);
    }

    public static PbrMaterial CreateColored(string name, Vector3 color, float metallic = 0.0f, float roughness = 0.5f) => new(name)
    {
        BaseColorFactor = new Vector4(color, 1.0f),
        MetallicFactor = metallic,
        RoughnessFactor = roughness
    };
}
