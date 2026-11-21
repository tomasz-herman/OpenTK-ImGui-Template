using ObjectOrientedOpenGL.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Graphics;

public readonly record struct PointLightData(Vector3 Position, Vector3 Color, float Intensity, float Range);

/// <summary>
/// Metallic-roughness PBR renderer supporting directional sun light, up to 4 point lights, and hemisphere ambient.
/// </summary>
public sealed class PbrRenderer : IDisposable
{
    public Shader Shader { get; }
    public Vector3 SunDirection { get; set; } = new Vector3(0.4f, 0.7f, 0.5f).Normalized();
    public Vector3 SunColor { get; set; } = new(1.0f, 0.96f, 0.90f);
    public float SunIntensity { get; set; } = 3.0f;
    public Vector3 AmbientSky { get; set; } = new(0.28f, 0.36f, 0.48f);
    public Vector3 AmbientGround { get; set; } = new(0.10f, 0.09f, 0.08f);

    public PbrRenderer()
    {
        Shader = new Shader(
            ("ObjectOrientedOpenGL.Resources.Shaders.pbr.vert", ShaderType.VertexShader),
            ("ObjectOrientedOpenGL.Resources.Shaders.pbr.frag", ShaderType.FragmentShader));
    }

    public void BeginFrame(Camera camera, ReadOnlySpan<PointLightData> pointLights = default)
    {
        Shader.Use();
        Shader.LoadMatrix4("uViewProj", camera.ProjectionViewMatrix);
        Shader.LoadFloat3("uCameraPos", camera.Position);
        Shader.LoadFloat3("uSunDir", SunDirection.Normalized());
        Shader.LoadFloat3("uSunColor", SunColor);
        Shader.LoadFloat("uSunIntensity", SunIntensity);
        Shader.LoadFloat3("uAmbientSky", AmbientSky);
        Shader.LoadFloat3("uAmbientGround", AmbientGround);

        int count = Math.Min(pointLights.Length, 4);
        Shader.LoadInteger("uPointLightCount", count);
        for (int i = 0; i < count; i++)
        {
            Shader.LoadFloat3($"uPointLightPos[{i}]", pointLights[i].Position);
            Shader.LoadFloat3($"uPointLightColor[{i}]", pointLights[i].Color);
            Shader.LoadFloat($"uPointLightIntensity[{i}]", pointLights[i].Intensity);
            Shader.LoadFloat($"uPointLightRange[{i}]", pointLights[i].Range);
        }
    }

    public void DrawMesh(Mesh mesh, Material material, Matrix4 worldTransform)
    {
        Shader.LoadMatrix4("uModel", worldTransform);
        var normalMat = new Matrix3(worldTransform).Inverted();
        normalMat.Transpose();
        Shader.LoadMatrix3("uNormalMatrix", normalMat);

        material.Bind(Shader);
        mesh.Bind();
        mesh.RenderIndexed();
    }

    public void DrawModel(Model model, Matrix4 worldTransform)
    {
        DrawNode(model.Root, worldTransform);
    }

    private void DrawNode(ModelNode node, Matrix4 parentWorld)
    {
        var world = node.LocalTransform * parentWorld;
        foreach (var prim in node.Primitives)
            DrawMesh(prim.Mesh, prim.Material, world);
        foreach (var child in node.Children)
            DrawNode(child, world);
    }

    public void Dispose()
    {
        Shader.Dispose();
    }
}
