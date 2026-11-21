using System.Numerics;
using System.Runtime.InteropServices;
using ImSharp;
using ObjectOrientedOpenGL.Core;
using ObjectOrientedOpenGL.Extra;
using ObjectOrientedOpenGL.Graphics;
using ObjectOrientedOpenGL.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SharpGLTF.Schema2;
using Camera = ObjectOrientedOpenGL.Core.Camera;
using Mesh = ObjectOrientedOpenGL.Core.Mesh;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace Template3D;

public class Program(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : ImGuiGameWindow(gameWindowSettings, nativeWindowSettings)
{
    private Camera Camera { get; set; } = null!;
    private World World { get; set; } = null!;
    private SceneRenderer SceneRenderer { get; set; } = null!;
    private SceneEditorPanel EditorPanel { get; set; } = null!;
    private FpsCounter FpsCounter { get; set; } = null!;
    private Billboard OriginLabel { get; set; } = null!;
    private Model? SampleGltfModel { get; set; }

    private readonly List<Mesh> _proceduralMeshes = [];
    private int _controlMode = 3;
    private int _projectionMode;
    private bool _showDemoWindow;

    public static void Main(string[] args)
    {
        var gwSettings = GameWindowSettings.Default;
        var nwSettings = NativeWindowSettings.Default;
        nwSettings.NumberOfSamples = 4;

        using var program = new Program(gwSettings, nwSettings);
        program.Title = "OpenTK-ImGui-Template — 3D Game Engine Lite";
        program.ClientSize = new Vector2i(1400, 860);
        program.Run();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        Camera = new Camera(
            new EditorControl(new Vector3(4.5f, 3.2f, 5.5f), new Vector3(0f, 1.0f, 0f)),
            new PerspectiveProjection { Aspect = (float)ClientSize.X / Math.Max(ClientSize.Y, 1) });

        World = new World();
        SceneRenderer = new SceneRenderer(withSky: true, withGroundPlane: true);
        EditorPanel = new SceneEditorPanel();
        FpsCounter = new FpsCounter();
        OriginLabel = new Billboard(Vector3.Zero, () => ImGui.TextColored(new ImVec4(1, 1, 1, 0.85f), "Origin"));

        BuildDemoScene();

        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
    }

    private void BuildDemoScene()
    {
        // 1. Directional Sun Light
        var sun = World.CreateEntity("Sun Light")
            .Add(new LightComponent { Type = LightType.Directional, Color = new Vector3(1.0f, 0.96f, 0.90f), Intensity = 3.2f });
        sun.Get<TransformComponent>().EulerAnglesDegrees = new Vector3(-45f, 35f, 0f);

        // 2. Hierarchical Scene Graph: Pedestal -> Rotating Hub -> Orbiting Metallic Spheres + Point Light
        var cubeMesh = Track(PrimitiveMeshes.CreateCube(1.0f, "PedestalCube"));
        var sphereMesh = Track(PrimitiveMeshes.CreateSphere(0.42f, 24, 32, "OrbSphere"));
        var torusMesh = Track(PrimitiveMeshes.CreateTorus(0.65f, 0.18f, 36, 20, "CenterTorus"));

        var pedestal = World.CreateEntity("Pedestal")
            .Add(new MeshRendererComponent(cubeMesh, PbrMaterial.CreateColored("PedestalMat", new Vector3(0.28f, 0.30f, 0.35f), metallic: 0.2f, roughness: 0.65f)));
        pedestal.Get<TransformComponent>().Position = new Vector3(0f, 0.25f, 0f);
        pedestal.Get<TransformComponent>().Scale = new Vector3(2.2f, 0.5f, 2.2f);

        var centerTorus = World.CreateEntity("Gold Torus", pedestal)
            .Add(new MeshRendererComponent(torusMesh, PbrMaterial.CreateColored("GoldMat", new Vector3(1.0f, 0.78f, 0.34f), metallic: 0.95f, roughness: 0.22f)))
            .Add(new RotatorComponent(new Vector3(0.3f, 1.0f, 0.2f), 0.9f));
        centerTorus.Get<TransformComponent>().Position = new Vector3(0f, 2.6f, 0f);
        centerTorus.Get<TransformComponent>().Scale = new Vector3(0.45f, 2.0f, 0.45f);

        var orbitPivot = World.CreateEntity("Orbit Pivot")
            .Add(new RotatorComponent(Vector3.UnitY, 0.7f));
        orbitPivot.Get<TransformComponent>().Position = new Vector3(0f, 1.4f, 0f);

        var chromeOrb = World.CreateEntity("Chrome Sphere", orbitPivot)
            .Add(new MeshRendererComponent(sphereMesh, PbrMaterial.CreateColored("ChromeMat", new Vector3(0.88f, 0.91f, 0.95f), metallic: 0.95f, roughness: 0.12f)));
        chromeOrb.Get<TransformComponent>().Position = new Vector3(2.1f, 0f, 0f);

        var crimsonOrb = World.CreateEntity("Crimson Sphere", orbitPivot)
            .Add(new MeshRendererComponent(sphereMesh, PbrMaterial.CreateColored("CrimsonMat", new Vector3(0.85f, 0.18f, 0.22f), metallic: 0.15f, roughness: 0.32f)));
        crimsonOrb.Get<TransformComponent>().Position = new Vector3(-2.1f, 0f, 0f);

        var pointLight = World.CreateEntity("Orbiting Point Light", orbitPivot)
            .Add(new LightComponent { Type = LightType.Point, Color = new Vector3(0.4f, 0.8f, 1.0f), Intensity = 10.0f, Range = 12.0f });
        pointLight.Get<TransformComponent>().Position = new Vector3(0f, 0.6f, 2.2f);

        // 3. Demonstrate SharpGLTF (.glb) loading & spawning into the ECS Scene Graph
        using var glbStream = CreateSampleGlbStream();
        SampleGltfModel = GltfModelLoader.LoadGlb(glbStream, "SharpGltfPyramid.glb");
        var gltfRoot = World.SpawnModel(SampleGltfModel, "glTF Pyramid");
        gltfRoot.Get<TransformComponent>().Position = new Vector3(3.0f, 0.0f, -1.5f);

        EditorPanel.SelectedEntity = centerTorus;
    }

    private Mesh Track(Mesh mesh)
    {
        _proceduralMeshes.Add(mesh);
        return mesh;
    }

    /// <summary>
    /// Creates an in-memory glTF 2.0 binary (.glb) asset using SharpGLTF to demonstrate
    /// GltfModelLoader without requiring external asset downloads.
    /// </summary>
    private static MemoryStream CreateSampleGlbStream()
    {
        var model = ModelRoot.CreateModel();
        var mat = model.CreateMaterial("EmeraldPBR");
        mat.InitializePBRMetallicRoughness();
        var baseCol = mat.FindChannel("BaseColor")!.Value;
        baseCol.Color = new System.Numerics.Vector4(0.15f, 0.78f, 0.45f, 1.0f);
        var mr = mat.FindChannel("MetallicRoughness")!.Value;
        mr.SetFactor("MetallicFactor", 0.35f);
        mr.SetFactor("RoughnessFactor", 0.28f);

        System.Numerics.Vector3[] positions =
        [
            new(-0.6f, 0f,  0.6f), new( 0.6f, 0f,  0.6f), new(0f, 1.2f, 0f),
            new( 0.6f, 0f,  0.6f), new( 0.6f, 0f, -0.6f), new(0f, 1.2f, 0f),
            new( 0.6f, 0f, -0.6f), new(-0.6f, 0f, -0.6f), new(0f, 1.2f, 0f),
            new(-0.6f, 0f, -0.6f), new(-0.6f, 0f,  0.6f), new(0f, 1.2f, 0f),
        ];
        uint[] indices = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

        var posAccessor = model.CreateAccessor();
        posAccessor.SetVertexData(model.UseBufferView(MemoryMarshal.AsBytes(positions.AsSpan()).ToArray()), 0, positions.Length, new SharpGLTF.Memory.AttributeFormat(DimensionType.VEC3));

        var idxAccessor = model.CreateAccessor();
        idxAccessor.SetIndexData(model.UseBufferView(MemoryMarshal.AsBytes(indices.AsSpan()).ToArray()), 0, indices.Length, IndexEncodingType.UNSIGNED_INT);

        var mesh = model.CreateMesh("PyramidMesh");
        var prim = mesh.CreatePrimitive();
        prim.Material = mat;
        prim.SetVertexAccessor("POSITION", posAccessor);
        prim.SetIndexAccessor(idxAccessor);

        var scene = model.UseScene("Default");
        var node = scene.CreateNode("PyramidNode");
        node.Mesh = mesh;

        var ms = new MemoryStream();
        model.WriteGLB(ms);
        ms.Position = 0;
        return ms;
    }

    protected override void OnUnload()
    {
        SampleGltfModel?.Dispose();
        foreach (var mesh in _proceduralMeshes)
            mesh.Dispose();
        SceneRenderer.Dispose();
        ResourcesManager.Instance.Dispose();
        base.OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        if (ClientSize.Y > 0)
            Camera.Aspect = (float)ClientSize.X / ClientSize.Y;
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        float dt = (float)args.Time;

        FpsCounter.Update(args.Time);
        World.Update(dt);
        Camera.Update(dt);

        var keyboard = KeyboardState.GetSnapshot();
        if (keyboard.IsKeyDown(Keys.Escape))
            Close();

        var io = ImGui.GetIO();
        if (io.WantCaptureMouse || EditorPanel.IsGizmoUsing || EditorPanel.IsGizmoOver)
            return;

        Camera.HandleInput(dt, keyboard, MouseState.GetSnapshot());
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        SceneRenderer.Render(World, Camera);
        RenderGui();

        Context.SwapBuffers();
    }

    protected override void BuildGuiLayout()
    {
        EditorPanel.Draw(World, Camera);
        DrawCameraPanel();
        FpsCounter.Render();
        OriginLabel.Render(Camera);

        if (_showDemoWindow)
            ImGui.ShowDemoWindow(ref _showDemoWindow);
    }

    private void DrawCameraPanel()
    {
        ImGui.SetNextWindowPos(new ImVec2(ClientSize.X - 270, 48), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(254, 230), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Camera & Scene"))
        {
            if (ImGui.CollapsingHeader("Control", ImGuiTreeNodeFlags.DefaultOpen))
            {
                using var indent = new ImGuiIndent(10f);
                if (ImGui.RadioButton("No Control", ref _controlMode, 0)) Camera.Control = new NoControl(Camera.Control);
                if (ImGui.RadioButton("Orbital", ref _controlMode, 1)) Camera.Control = new OrbitalControl(Camera.Control);
                if (ImGui.RadioButton("FlyBy", ref _controlMode, 2)) Camera.Control = new FlyByControl(Camera.Control);
                if (ImGui.RadioButton("Editor", ref _controlMode, 3)) Camera.Control = new EditorControl(Camera.Control);
            }

            if (ImGui.CollapsingHeader("Projection"))
            {
                using var indent = new ImGuiIndent(10f);
                if (ImGui.RadioButton("Perspective", ref _projectionMode, 0)) Camera.Projection = new PerspectiveProjection { Aspect = Camera.Aspect };
                if (ImGui.RadioButton("Orthographic", ref _projectionMode, 1)) Camera.Projection = new OrthographicProjection { Aspect = Camera.Aspect };
            }

            ImGui.Separator();
            ImGui.Checkbox("ImGui Demo Window", ref _showDemoWindow);
        }
        ImGui.End();
    }
}
