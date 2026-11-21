using System.Runtime.InteropServices;
using ObjectOrientedOpenGL.Canvas2D;
using ObjectOrientedOpenGL.Graphics;
using ObjectOrientedOpenGL.Physics;
using ObjectOrientedOpenGL.Scene;
using OpenTK.Mathematics;
using SharpGLTF.Schema2;
using Xunit;

namespace ObjectOrientedOpenGL.Tests;

public class EngineTests
{
    [Fact]
    public void Ecs_World_Lifecycle_Components_And_Queries()
    {
        var world = new World();
        var e1 = world.CreateEntity("Hero");
        var e2 = world.CreateEntity("Light")
            .Add(new LightComponent { Type = LightType.Point, Intensity = 5f });

        Assert.Equal(2, world.EntityCount);
        Assert.True(e1.IsValid);
        Assert.True(e2.Has<LightComponent>());
        Assert.False(e1.Has<LightComponent>());

        var lights = world.Query<TransformComponent, LightComponent>().ToList();
        Assert.Single(lights);
        Assert.Equal(e2, lights[0].Entity);
        Assert.Equal(5f, lights[0].C2.Intensity);

        e2.Destroy();
        Assert.False(e2.IsValid);
        Assert.Equal(1, world.EntityCount);
    }

    [Fact]
    public void SceneGraph_TransformSystem_Propagates_Hierarchy_WorldMatrices()
    {
        var world = new World();
        var parent = world.CreateEntity("Parent");
        parent.Get<TransformComponent>().Position = new Vector3(10, 0, 0);

        var child = world.CreateEntity("Child", parent);
        child.Get<TransformComponent>().Position = new Vector3(0, 5, 0);

        var grandChild = world.CreateEntity("GrandChild", child);
        grandChild.Get<TransformComponent>().Position = new Vector3(0, 0, 3);

        world.Update(0f);

        Assert.Equal(new Vector3(10, 0, 0), parent.Get<TransformComponent>().WorldPosition);
        Assert.Equal(new Vector3(10, 5, 0), child.Get<TransformComponent>().WorldPosition);
        Assert.Equal(new Vector3(10, 5, 3), grandChild.Get<TransformComponent>().WorldPosition);

        // Destroying parent cascades to children
        parent.Destroy();
        Assert.Equal(0, world.EntityCount);
        Assert.False(child.IsValid);
        Assert.False(grandChild.IsValid);
    }

    [Fact]
    public void PixelCanvas_GetSetPixel_And_Drawing_Primitives()
    {
        using var canvas = new PixelCanvas(64, 48);
        canvas.Fill(Color4.Black);

        canvas.SetPixel(10, 20, Color4.Red);
        var px = canvas.GetPixel(10, 20);
        Assert.Equal(1f, px.R, 2);
        Assert.Equal(0f, px.G, 2);
        Assert.Equal(0f, px.B, 2);

        // Out-of-bounds is safely ignored
        canvas.SetPixel(-1, 999, Color4.White);
        Assert.Equal(default, canvas.GetPixel(-1, 999));

        // Drawing helpers
        canvas.FillRect(0, 0, 4, 4, Color4.Lime);
        Assert.Equal(1f, canvas.GetPixel(3, 3).G, 2);

        canvas.DrawLine(0, 10, 10, 10, Color4.Blue);
        Assert.Equal(1f, canvas.GetPixel(5, 10).B, 2);

        canvas.FillCircle(30, 30, 5, Color4.Yellow);
        Assert.Equal(1f, canvas.GetPixel(30, 30).R, 2);
        Assert.Equal(1f, canvas.GetPixel(30, 30).G, 2);

        // Resize with preserve
        canvas.Resize(80, 60, preserve: true);
        Assert.Equal(80, canvas.Width);
        Assert.Equal(60, canvas.Height);
        Assert.Equal(1f, canvas.GetPixel(10, 20).R, 2);
    }

    [Fact]
    public void GltfModelLoader_Rejects_NonGltf_Formats()
    {
        Assert.Throws<NotSupportedException>(() => GltfModelLoader.ValidateExtension("cube.obj"));
        Assert.Throws<NotSupportedException>(() => GltfModelLoader.ValidateExtension("hero.fbx"));
        GltfModelLoader.ValidateExtension("model.gltf");
        GltfModelLoader.ValidateExtension("model.glb");
    }

    [Fact]
    public void GltfModelLoader_Parses_SharpGltf_Glb_Hierarchy_And_PbrMaterial()
    {
        var model = ModelRoot.CreateModel();
        var mat = model.CreateMaterial("Gold");
        mat.InitializePBRMetallicRoughness();
        var baseCol = mat.FindChannel("BaseColor")!.Value;
        baseCol.Color = new System.Numerics.Vector4(1f, 0.8f, 0.2f, 1f);
        var mr = mat.FindChannel("MetallicRoughness")!.Value;
        mr.SetFactor("MetallicFactor", 0.9f);
        mr.SetFactor("RoughnessFactor", 0.25f);

        System.Numerics.Vector3[] positions =
        [
            new(0, 0, 0),
            new(1, 0, 0),
            new(0, 1, 0)
        ];
        uint[] indices = [0, 1, 2];

        var posAccessor = model.CreateAccessor();
        posAccessor.SetVertexData(model.UseBufferView(MemoryMarshal.AsBytes(positions.AsSpan()).ToArray()), 0, positions.Length, new SharpGLTF.Memory.AttributeFormat(DimensionType.VEC3));

        var idxAccessor = model.CreateAccessor();
        idxAccessor.SetIndexData(model.UseBufferView(MemoryMarshal.AsBytes(indices.AsSpan()).ToArray()), 0, indices.Length, IndexEncodingType.UNSIGNED_INT);

        var mesh = model.CreateMesh("Triangle");
        var prim = mesh.CreatePrimitive();
        prim.Material = mat;
        prim.SetVertexAccessor("POSITION", posAccessor);
        prim.SetIndexAccessor(idxAccessor);

        var scene = model.UseScene("Main");
        var parentNode = scene.CreateNode("RootNode");
        parentNode.LocalTransform = SharpGLTF.Transforms.AffineTransform.CreateFromAny(
            null, null, null, new System.Numerics.Vector3(2, 3, 4));
        var childNode = parentNode.CreateNode("ChildMesh");
        childNode.Mesh = mesh;

        using var ms = new MemoryStream();
        model.WriteGLB(ms);
        ms.Position = 0;

        var parsed = GltfModelLoader.ParseGlbCpu(ms);

        Assert.Single(parsed.Materials);
        Assert.Equal("Gold", parsed.Materials[0].Name);
        Assert.Equal(0.9f, parsed.Materials[0].MetallicFactor, 3);
        Assert.Equal(0.25f, parsed.Materials[0].RoughnessFactor, 3);

        Assert.Single(parsed.Meshes);
        Assert.Single(parsed.Meshes[0]);
        Assert.Equal(3, parsed.Meshes[0][0].Vertices.Length);
        Assert.Equal(3, parsed.Meshes[0][0].Indices.Length);
        // Smooth normal auto-generated along +Z
        Assert.True(parsed.Meshes[0][0].Vertices[0].Normal.Z > 0.9f);

        Assert.Single(parsed.Root.Children);
        Assert.Equal("RootNode", parsed.Root.Children[0].Name);
        Assert.Single(parsed.Root.Children[0].Children);
        Assert.Equal("ChildMesh", parsed.Root.Children[0].Children[0].Name);
    }

    [Fact]
    public void BepuPhysics2_DynamicBody_Falls_And_Lands_On_StaticFloor()
    {
        var world = new World();
        using var physics = new PhysicsWorld(new Vector3(0f, -9.81f, 0f), threadCount: 1);
        world.AddSystem(physics);

        var floor = world.CreateEntity("Floor")
            .Add(RigidBodyComponent.Static())
            .Add(ColliderComponent.Box(50f, 1f, 50f));
        floor.Get<TransformComponent>().Position = new Vector3(0f, -0.5f, 0f);

        var ball = world.CreateEntity("Ball")
            .Add(RigidBodyComponent.Dynamic(mass: 1.0f))
            .Add(ColliderComponent.Sphere(0.5f));
        ball.Get<TransformComponent>().Position = new Vector3(0f, 4.0f, 0f);

        // Simulate 2 seconds at 60 Hz
        for (int i = 0; i < 120; i++)
            world.Update(1f / 60f);

        Assert.Equal(1, physics.BodyCount);
        Assert.Equal(1, physics.StaticCount);

        float y = ball.Get<TransformComponent>().Position.Y;
        // Sphere of radius 0.5 resting on floor at y=0 should settle near y=0.5
        Assert.InRange(y, 0.4f, 0.75f);

        // Raycast downward should hit the ball first
        bool hit = physics.Raycast(world, new Vector3(0f, 10f, 0f), -Vector3.UnitY, 20f, out var rayHit);
        Assert.True(hit);
        Assert.Equal(ball, rayHit.Entity);
        Assert.True(rayHit.Distance > 8f && rayHit.Distance < 10f);

        // Destroying entity removes its Bepu body on next update
        ball.Destroy();
        world.Update(1f / 60f);
        Assert.Equal(0, physics.BodyCount);
    }

    [Fact]
    public async Task GltfModelLoader_PrepareGlbAsync_Reports_Progress_And_Parses_OffThread()
    {
        var model = ModelRoot.CreateModel();
        var mesh = model.CreateMesh("AsyncTri");
        var prim = mesh.CreatePrimitive();
        System.Numerics.Vector3[] positions = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];
        uint[] indices = [0, 1, 2];

        var posAcc = model.CreateAccessor();
        posAcc.SetVertexData(model.UseBufferView(MemoryMarshal.AsBytes(positions.AsSpan()).ToArray()), 0, 3, new SharpGLTF.Memory.AttributeFormat(DimensionType.VEC3));
        var idxAcc = model.CreateAccessor();
        idxAcc.SetIndexData(model.UseBufferView(MemoryMarshal.AsBytes(indices.AsSpan()).ToArray()), 0, 3, IndexEncodingType.UNSIGNED_INT);
        prim.SetVertexAccessor("POSITION", posAcc);
        prim.SetIndexAccessor(idxAcc);

        var scene = model.UseScene("Main");
        scene.CreateNode("Root").Mesh = mesh;

        using var ms = new MemoryStream();
        model.WriteGLB(ms);
        ms.Position = 0;

        var reports = new List<ObjectOrientedOpenGL.Core.AssetLoadProgress>();
        var progress = new Progress<ObjectOrientedOpenGL.Core.AssetLoadProgress>(p => { lock (reports) reports.Add(p); });

        var prepared = await GltfModelLoader.PrepareGlbAsync(ms, "AsyncTri.glb", progress);
        Assert.Equal("AsyncTri.glb", prepared.Path);
        Assert.Single(prepared.Parsed.Meshes);
    }

    [Fact]
    public void AnimationSystem_Evaluates_Keyframes_And_Updates_Transform()
    {
        var world = new World();
        var node = world.CreateEntity("AnimatedNode");

        var channel = new ObjectOrientedOpenGL.Graphics.AnimationChannel(
            targetNodeIndex: 0,
            AnimationPath.Translation,
            AnimationInterpolation.Linear,
            times: [0f, 1f],
            vec3Values: [new Vector3(0, 0, 0), new Vector3(10, 0, 0)]);

        var clip = new AnimationClip("Slide", 1.0f, [channel]);
        var animator = new AnimatorComponent([clip], [], new Dictionary<int, Entity> { [0] = node });
        node.Add(animator);

        world.Update(0.5f);
        Assert.Equal(5.0f, node.Get<TransformComponent>().Position.X, 2);

        world.Update(0.5f);
        // Loops back to 0.0 at t=1.0
        Assert.Equal(0.0f, node.Get<TransformComponent>().Position.X, 2);
    }

    [Fact]
    public void GltfModelLoader_Parses_Sponza_When_Asset_Present()
    {
        string? dir = AppContext.BaseDirectory;
        string? sponzaPath = null;
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            string candidate = Path.Combine(dir, "Template3D", "Assets", "IntelSponza", "NewSponza_Main_glTF_003.gltf");
            if (File.Exists(candidate))
            {
                sponzaPath = candidate;
                break;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (sponzaPath is null)
            return;

        var parsed = GltfModelLoader.ParseCpu(sponzaPath);
        Assert.NotEmpty(parsed.Meshes);
        Assert.NotEmpty(parsed.Materials);
        Assert.NotEmpty(parsed.Images);
        Assert.All(parsed.Images, bytes => Assert.NotEmpty(bytes));
    }
}
