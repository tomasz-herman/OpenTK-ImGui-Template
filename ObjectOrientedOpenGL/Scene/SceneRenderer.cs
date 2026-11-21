using ObjectOrientedOpenGL.Core;
using ObjectOrientedOpenGL.Extra;
using ObjectOrientedOpenGL.Graphics;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Scene;

/// <summary>
/// Renders an ECS <see cref="World"/> using <see cref="PbrRenderer"/>, optional <see cref="Sky"/>, and <see cref="Plane"/>.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    public PbrRenderer Pbr { get; } = new();
    public Sky? Sky { get; set; }
    public Plane? GroundPlane { get; set; }
    public bool ShowSky { get; set; } = true;
    public bool ShowGroundPlane { get; set; } = true;

    public SceneRenderer(bool withSky = true, bool withGroundPlane = true)
    {
        if (withSky) Sky = new Sky();
        if (withGroundPlane) GroundPlane = new Plane();
    }

    public void Render(World world, Camera camera)
    {
        world.TransformSystem.Update(world, 0f);

        // Gather lights from ECS
        Span<PointLightData> pointLights = stackalloc PointLightData[4];
        int pointLightCount = 0;
        bool hasDirectional = false;

        foreach (var (entity, transform, light) in world.Query<TransformComponent, LightComponent>())
        {
            if (entity.TryGet<NameComponent>(out var name) && name is { Active: false })
                continue;

            if (light.Type == LightType.Directional && !hasDirectional)
            {
                hasDirectional = true;
                var dir = Vector3.Transform(-Vector3.UnitZ, transform.WorldMatrix.ExtractRotation(true)).Normalized();
                Pbr.SunDirection = dir;
                Pbr.SunColor = light.Color;
                Pbr.SunIntensity = light.Intensity;
                if (Sky is not null)
                {
                    Sky.SunDir = dir;
                    Sky.Update();
                }
            }
            else if (light.Type == LightType.Point && pointLightCount < 4)
            {
                pointLights[pointLightCount++] = new PointLightData(
                    transform.WorldPosition,
                    light.Color,
                    light.Intensity,
                    light.Range);
            }
        }

        if (ShowSky) Sky?.Render(camera);
        if (ShowGroundPlane) GroundPlane?.Render(camera);

        Pbr.BeginFrame(camera, pointLights[..pointLightCount]);

        foreach (var (entity, transform, renderer) in world.Query<TransformComponent, MeshRendererComponent>())
        {
            if (!renderer.Visible) continue;
            if (entity.TryGet<NameComponent>(out var name) && name is { Active: false }) continue;

            Pbr.DrawMesh(renderer.Mesh, renderer.Material, transform.WorldMatrix);
        }
    }

    public void Dispose()
    {
        Pbr.Dispose();
        Sky?.Dispose();
        GroundPlane?.Dispose();
    }
}
