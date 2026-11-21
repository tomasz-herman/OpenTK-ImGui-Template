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

namespace Template3D;

public class Program(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, string? initialModelPath)
    : ImGuiGameWindow(gameWindowSettings, nativeWindowSettings)
{
    private Camera Camera { get; set; } = null!;
    private World World { get; set; } = null!;
    private SceneRenderer SceneRenderer { get; set; } = null!;
    private SceneEditorPanel EditorPanel { get; set; } = null!;
    private FpsCounter FpsCounter { get; set; } = null!;
    private Billboard OriginLabel { get; set; } = null!;
    private Model? LoadedModel { get; set; }

    private readonly List<Mesh> _proceduralMeshes = [];
    private readonly string? _initialModelPath = initialModelPath;
    private readonly byte[] _gltfPathBuf = new byte[512];
    private string _sceneStatus = string.Empty;
    private bool _isGltfScene;
    private int _controlMode = 2;
    private int _projectionMode;
    private float _flySpeed = 5.0f;
    private bool _showOriginLabel;
    private bool _showDemoWindow;

    public static void Main(string[] args)
    {
        string? cliModel = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;

        var gwSettings = GameWindowSettings.Default;
        var nwSettings = NativeWindowSettings.Default;
        nwSettings.NumberOfSamples = 4;

        using var program = new Program(gwSettings, nwSettings, cliModel);
        program.Title = "OpenTK-ImGui-Template — 3D Engine (glTF 2.0 / Sponza)";
        program.ClientSize = new Vector2i(1440, 900);
        program.Run();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        Camera = new Camera(
            new FlyByControl(new Vector3(-7.0f, 2.0f, 0.0f), new Vector3(4.0f, 2.8f, 0.0f)) { Speed = _flySpeed },
            new PerspectiveProjection { Aspect = (float)ClientSize.X / Math.Max(ClientSize.Y, 1), Near = 0.1f, Far = 300.0f });

        World = new World();
        SceneRenderer = new SceneRenderer(withSky: true, withGroundPlane: true);
        EditorPanel = new SceneEditorPanel();
        FpsCounter = new FpsCounter();
        OriginLabel = new Billboard(Vector3.Zero, () => ImGui.TextColored(new ImVec4(1, 1, 1, 0.85f), "Origin"));

        string? modelPath = _initialModelPath ?? FindDefaultSponzaPath();
        if (modelPath != null && File.Exists(modelPath))
        {
            SetPathInput(modelPath);
            LoadGltfScene(modelPath);
        }
        else
        {
            SetPathInput(Path.Combine("Template3D", "Assets", "Sponza", "Sponza.gltf"));
            BuildProceduralDemoScene();
        }

        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
    }

    private static string? FindDefaultSponzaPath()
    {
        string[] relativeCandidates =
        [
            Path.Combine("Assets", "Sponza", "NewSponza_Main_glTF_003.gltf"),
            Path.Combine("Assets", "Sponza", "Sponza.gltf"),
            Path.Combine("Template3D", "Assets", "Sponza", "NewSponza_Main_glTF_003.gltf"),
            Path.Combine("Template3D", "Assets", "Sponza", "Sponza.gltf"),
        ];

        string[] roots =
        [
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
        ];

        foreach (string root in roots)
        {
            string? dir = root;
            for (int depth = 0; depth < 5 && !string.IsNullOrEmpty(dir); depth++)
            {
                foreach (string rel in relativeCandidates)
                {
                    string full = Path.GetFullPath(Path.Combine(dir, rel));
                    if (File.Exists(full))
                        return full;
                }
                dir = Directory.GetParent(dir)?.FullName;
            }
        }

        return null;
    }

    private void ClearScene()
    {
        EditorPanel.SelectedEntity = null;
        World.Clear();
        LoadedModel?.Dispose();
        LoadedModel = null;
        foreach (var mesh in _proceduralMeshes)
            mesh.Dispose();
        _proceduralMeshes.Clear();
    }

    private void LoadGltfScene(string path)
    {
        try
        {
            var model = GltfModelLoader.Load(path);
            ClearScene();
            LoadedModel = model;
            _isGltfScene = true;
            _showOriginLabel = false;
            SceneRenderer.ShowGroundPlane = false;
            SceneRenderer.ShowSky = true;

            // Courtyard + interior bounce ambient
            SceneRenderer.Pbr.AmbientSky = new Vector3(0.45f, 0.52f, 0.65f);
            SceneRenderer.Pbr.AmbientGround = new Vector3(0.22f, 0.19f, 0.16f);

            var modelRoot = World.SpawnModel(model);
            var bounds = model.Bounds;
            var center = bounds.Center;
            var size = bounds.Size;
            float diag = MathF.Max(size.Length, 1.0f);

            // High directional sun angling into the courtyard
            var sun = World.CreateEntity("Sun Light")
                .Add(new LightComponent { Type = LightType.Directional, Color = new Vector3(1.0f, 0.96f, 0.88f), Intensity = 4.2f });
            sun.Get<TransformComponent>().EulerAnglesDegrees = new Vector3(-62f, 22f, 0f);

            // Warm interior point lights along the main axis
            float lightY = bounds.Min.Y + MathF.Min(3.2f, size.Y * 0.28f);
            float lightRange = MathF.Max(14.0f, size.X * 0.55f);
            float lightIntensity = MathF.Max(18.0f, size.X * 1.1f);
            float spanX = size.X * 0.28f;

            Vector3[] lightPositions =
            [
                new(center.X - spanX, lightY, center.Z),
                new(center.X, lightY + 0.8f, center.Z),
                new(center.X + spanX, lightY, center.Z),
            ];

            for (int i = 0; i < lightPositions.Length; i++)
            {
                var pt = World.CreateEntity($"Atrium Light {i + 1}")
                    .Add(new LightComponent
                    {
                        Type = LightType.Point,
                        Color = new Vector3(1.0f, 0.86f, 0.68f),
                        Intensity = lightIntensity,
                        Range = lightRange
                    });
                pt.Get<TransformComponent>().Position = lightPositions[i];
            }

            // Place FlyBy camera inside the atrium looking down the long axis
            Vector3 camPos = new(
                center.X - size.X * 0.28f,
                bounds.Min.Y + MathF.Min(2.0f, size.Y * 0.18f),
                center.Z);
            Vector3 camTarget = new(
                center.X + size.X * 0.20f,
                bounds.Min.Y + MathF.Min(2.8f, size.Y * 0.26f),
                center.Z);

            _flySpeed = Math.Clamp(diag * 0.14f, 1.5f, 15.0f);
            _controlMode = 2;
            Camera.Control = new FlyByControl(camPos, camTarget) { Speed = _flySpeed };
            if (Camera.Projection is PerspectiveProjection persp)
            {
                persp.Near = MathF.Max(0.05f, diag * 0.002f);
                persp.Far = MathF.Max(300.0f, diag * 10.0f);
            }

            Title = $"OpenTK-ImGui-Template — {Path.GetFileName(path)}";
            _sceneStatus = $"Loaded {Path.GetFileName(path)}: {model.Primitives.Count} meshes, {model.Materials.Count} materials, {model.Textures.Count} textures";
            _ = modelRoot;
        }
        catch (Exception ex)
        {
            _sceneStatus = $"Load failed: {ex.Message}";
        }
    }

    private void BuildProceduralDemoScene()
    {
        ClearScene();
        _isGltfScene = false;
        _showOriginLabel = true;
        SceneRenderer.ShowGroundPlane = true;
        SceneRenderer.ShowSky = true;
        SceneRenderer.Pbr.AmbientSky = new Vector3(0.28f, 0.36f, 0.48f);
        SceneRenderer.Pbr.AmbientGround = new Vector3(0.10f, 0.09f, 0.08f);

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
        LoadedModel = GltfModelLoader.LoadGlb(glbStream, "SharpGltfPyramid.glb");
        var gltfRoot = World.SpawnModel(LoadedModel, "glTF Pyramid");
        gltfRoot.Get<TransformComponent>().Position = new Vector3(3.0f, 0.0f, -1.5f);

        _controlMode = 3;
        Camera.Control = new EditorControl(new Vector3(4.5f, 3.2f, 5.5f), new Vector3(0f, 1.0f, 0f));
        EditorPanel.SelectedEntity = centerTorus;
        Title = "OpenTK-ImGui-Template — 3D Game Engine Lite";
        _sceneStatus = "Procedural PBR Demo";
    }

    private void SetPathInput(string path)
    {
        Array.Clear(_gltfPathBuf);
        int written = System.Text.Encoding.UTF8.GetBytes(path.AsSpan(), _gltfPathBuf.AsSpan(0, _gltfPathBuf.Length - 1));
        _gltfPathBuf[written] = 0;
    }

    private string GetPathInput()
    {
        int len = Array.IndexOf(_gltfPathBuf, (byte)0);
        if (len < 0) len = _gltfPathBuf.Length;
        return System.Text.Encoding.UTF8.GetString(_gltfPathBuf, 0, len).Trim();
    }

    private Mesh Track(Mesh mesh)
    {
        _proceduralMeshes.Add(mesh);
        return mesh;
    }

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
        ClearScene();
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
        if (io.WantCaptureMouse || io.WantCaptureKeyboard || EditorPanel.IsGizmoUsing || EditorPanel.IsGizmoOver)
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
        if (_showOriginLabel)
            OriginLabel.Render(Camera);

        if (_showDemoWindow)
            ImGui.ShowDemoWindow(ref _showDemoWindow);
    }

    private void DrawCameraPanel()
    {
        ImGui.SetNextWindowPos(new ImVec2(ClientSize.X - 310, 48), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(294, 370), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Camera & Scene"))
        {
            if (ImGui.CollapsingHeader("Scene", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Button("Sponza"))
                {
                    string? sponza = FindDefaultSponzaPath();
                    if (sponza != null)
                    {
                        SetPathInput(sponza);
                        LoadGltfScene(sponza);
                    }
                    else
                    {
                        _sceneStatus = "Sponza.gltf not found in Template3D/Assets/Sponza/";
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Procedural Demo"))
                    BuildProceduralDemoScene();

                ImGui.InputText("##gltfPath", _gltfPathBuf, (nuint)_gltfPathBuf.Length);
                ImGui.SameLine();
                if (ImGui.Button("Load"))
                    LoadGltfScene(GetPathInput());

                if (!string.IsNullOrEmpty(_sceneStatus))
                    ImGui.TextWrapped(_sceneStatus);

                if (_isGltfScene && LoadedModel is { } m)
                {
                    var sz = m.Bounds.Size;
                    ImGui.TextDisabled($"Bounds: {sz.X:F1} x {sz.Y:F1} x {sz.Z:F1} m");
                }
            }

            if (ImGui.CollapsingHeader("Control", ImGuiTreeNodeFlags.DefaultOpen))
            {
                using var indent = new ImGuiIndent(10f);
                if (ImGui.RadioButton("No Control", ref _controlMode, 0)) Camera.Control = new NoControl(Camera.Control);
                if (ImGui.RadioButton("Orbital", ref _controlMode, 1)) Camera.Control = new OrbitalControl(Camera.Control);
                if (ImGui.RadioButton("FlyBy (WASD+Shift)", ref _controlMode, 2)) Camera.Control = new FlyByControl(Camera.Control) { Speed = _flySpeed };
                if (ImGui.RadioButton("Editor", ref _controlMode, 3)) Camera.Control = new EditorControl(Camera.Control);

                if (Camera.Control is FlyByControl fly)
                {
                    if (ImGui.SliderFloat("Fly Speed", ref _flySpeed, 0.5f, 25f))
                        fly.Speed = _flySpeed;
                }
            }

            if (ImGui.CollapsingHeader("Rendering"))
            {
                using var indent = new ImGuiIndent(10f);
                bool showSky = SceneRenderer.ShowSky;
                if (ImGui.Checkbox("Show Sky", ref showSky)) SceneRenderer.ShowSky = showSky;

                bool showGround = SceneRenderer.ShowGroundPlane;
                if (ImGui.Checkbox("Show Ground Plane", ref showGround)) SceneRenderer.ShowGroundPlane = showGround;

                if (ImGui.RadioButton("Perspective", ref _projectionMode, 0)) Camera.Projection = new PerspectiveProjection { Aspect = Camera.Aspect };
                if (ImGui.RadioButton("Orthographic", ref _projectionMode, 1)) Camera.Projection = new OrthographicProjection { Aspect = Camera.Aspect };
            }

            ImGui.Separator();
            ImGui.Checkbox("ImGui Demo Window", ref _showDemoWindow);
        }
        ImGui.End();
    }
}
