using ImSharp;
using ObjectOrientedOpenGL.Core;
using ObjectOrientedOpenGL.Extra;
using ObjectOrientedOpenGL.Graphics;
using ObjectOrientedOpenGL.Physics;
using ObjectOrientedOpenGL.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Camera = ObjectOrientedOpenGL.Core.Camera;
using Mesh = ObjectOrientedOpenGL.Core.Mesh;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace Template3D;

public class Program(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, string? initialModelPath)
    : ImGuiGameWindow(gameWindowSettings, nativeWindowSettings)
{
    private Camera Camera { get; set; } = null!;
    private World World { get; set; } = null!;
    private PhysicsWorld Physics { get; set; } = null!;
    private AssetLoader AssetLoader { get; set; } = null!;
    private SceneRenderer SceneRenderer { get; set; } = null!;
    private SceneEditorPanel EditorPanel { get; set; } = null!;
    private FpsCounter FpsCounter { get; set; } = null!;
    private Model? LoadedModel { get; set; }
    private Entity? _sunEntity;
    private Entity? _animatedCharacterEntity;

    private Mesh _physicsCubeMesh = null!;
    private Mesh _physicsSphereMesh = null!;
    private int _spawnCounter;
    private readonly List<Model> _addonModels = [];
    private readonly List<Mesh> _proceduralMeshes = [];
    private readonly string? _initialModelPath = initialModelPath;
    private readonly byte[] _gltfPathBuf = new byte[512];
    private string _sceneStatus = string.Empty;
    private bool _isGltfScene;

    // Camera settings
    private int _controlMode = 2;
    private int _projectionMode;
    private float _flySpeed = 5.0f;
    private float _mouseSensitivity = 0.003f;

    // Sky & Day/Night cycle settings
    private float _timeOfDayHours = 13.5f;
    private float _sunAzimuthDeg = 28.0f;
    private float _sunIntensityScale = 4.2f;
    private bool _autoDayNightCycle;
    private float _dayNightSpeedHoursPerSec = 1.0f;

    private bool _showDemoWindow;

    public static void Main(string[] args)
    {
        string? cliModel = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;

        var gwSettings = GameWindowSettings.Default;
        var nwSettings = NativeWindowSettings.Default;
        nwSettings.NumberOfSamples = 4;

        using var program = new Program(gwSettings, nwSettings, cliModel);
        program.Title = "OpenTK-ImGui-Template — 3D Engine (glTF 2.0 / Intel Sponza)";
        program.ClientSize = new Vector2i(1440, 900);
        program.Run();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        Camera = new Camera(
            new FlyByControl(new Vector3(-7.0f, 2.0f, 0.0f), new Vector3(4.0f, 2.5f, 0.0f))
            {
                Speed = _flySpeed,
                RotationSpeed = _mouseSensitivity
            },
            new PerspectiveProjection { Aspect = (float)ClientSize.X / Math.Max(ClientSize.Y, 1), Near = 0.1f, Far = 300.0f });

        World = new World();
        Physics = new PhysicsWorld();
        AssetLoader = new AssetLoader();
        World.AddSystem(Physics);
        SceneRenderer = new SceneRenderer(withSky: true, withGroundPlane: true);
        EditorPanel = new SceneEditorPanel { Physics = Physics };
        FpsCounter = new FpsCounter();
        _physicsCubeMesh = PrimitiveMeshes.CreateCube(1.0f, "PhysicsCrate");
        _physicsSphereMesh = PrimitiveMeshes.CreateSphere(0.45f, 20, 28, "PhysicsSphere");

        string? modelPath = _initialModelPath ?? FindDefaultSponzaPath();
        if (modelPath != null && File.Exists(modelPath))
        {
            SetPathInput(modelPath);
            LoadGltfScene(modelPath);
        }
        else
        {
            SetPathInput(Path.Combine("Template3D", "Assets", "IntelSponza", "NewSponza_Main_glTF_003.gltf"));
            BuildProceduralDemoScene();
        }

        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
    }

    private static string? FindAssetPath(params string[] relativeCandidates)
    {
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

    private static string? FindDefaultSponzaPath() => FindAssetPath(
        Path.Combine("Assets", "IntelSponza", "NewSponza_Main_glTF_003.gltf"),
        Path.Combine("Template3D", "Assets", "IntelSponza", "NewSponza_Main_glTF_003.gltf"));

    private static string? FindAnimatedCharacterPath() => FindAssetPath(
        Path.Combine("Assets", "AnimatedCharacter", "Fox.glb"),
        Path.Combine("Template3D", "Assets", "AnimatedCharacter", "Fox.glb"));

    private void ClearScene()
    {
        AssetLoader.CancelAll();
        EditorPanel.SelectedEntity = null;
        _sunEntity = null;
        _animatedCharacterEntity = null;
        Physics.Reset();
        World.Clear();
        LoadedModel?.Dispose();
        LoadedModel = null;
        foreach (var addon in _addonModels)
            addon.Dispose();
        _addonModels.Clear();
        foreach (var mesh in _proceduralMeshes)
            mesh.Dispose();
        _proceduralMeshes.Clear();
        _spawnCounter = 0;
    }

    private void AddStaticFloorCollider(float floorY)
    {
        var floor = World.CreateEntity("Physics Floor")
            .Add(RigidBodyComponent.Static())
            .Add(ColliderComponent.Box(200f, 1f, 200f));
        floor.Get<TransformComponent>().Position = new Vector3(0f, floorY - 0.5f, 0f);
    }

    private void SpawnPhysicsCrate(Vector3 position, Vector3? color = null)
    {
        int id = ++_spawnCounter;
        var col = color ?? new Vector3(0.76f, 0.48f, 0.26f);
        var crate = World.CreateEntity($"Crate #{id}")
            .Add(new MeshRendererComponent(_physicsCubeMesh, PbrMaterial.CreateColored($"CrateMat{id}", col, metallic: 0.08f, roughness: 0.62f)))
            .Add(RigidBodyComponent.Dynamic(mass: 2.0f))
            .Add(ColliderComponent.Box(Vector3.One));
        crate.Get<TransformComponent>().Position = position;
    }

    private void SpawnPhysicsSphere(Vector3 position, Vector3 initialVelocity = default)
    {
        int id = ++_spawnCounter;
        var rb = RigidBodyComponent.Dynamic(mass: 1.5f);
        if (initialVelocity.LengthSquared > 1e-4f)
            rb.SetLinearVelocity(initialVelocity);

        var sphere = World.CreateEntity($"Sphere #{id}")
            .Add(new MeshRendererComponent(_physicsSphereMesh, PbrMaterial.CreateColored($"SphereMat{id}", new Vector3(0.25f, 0.72f, 0.92f), metallic: 0.85f, roughness: 0.2f)))
            .Add(rb)
            .Add(ColliderComponent.Sphere(0.45f));
        sphere.Get<TransformComponent>().Position = position;
    }

    private void SpawnAnimatedCharacterAsync(Vector3 floorPosition, float targetHeight = 1.25f, float yawDegrees = 90f)
    {
        string? charPath = FindAnimatedCharacterPath();
        if (charPath is null) return;

        AssetLoader.LoadModelAsync(charPath, charModel =>
        {
            _addonModels.Add(charModel);

            var entity = World.SpawnModel(charModel, Path.GetFileNameWithoutExtension(charPath));
            float h = MathF.Max(charModel.Bounds.Size.Y, 0.001f);
            float scale = targetHeight / h;

            var tr = entity.Get<TransformComponent>();
            tr.Scale = new Vector3(scale);
            tr.EulerAnglesDegrees = new Vector3(0f, yawDegrees, 0f);
            tr.Position = new Vector3(
                floorPosition.X,
                floorPosition.Y - charModel.Bounds.Min.Y * scale,
                floorPosition.Z);

            if (entity.TryGet<AnimatorComponent>(out var anim) && anim is not null && anim.Clips.Count > 1)
                anim.ActiveClipIndex = 1; // Default to "Walk" on Fox.glb

            _animatedCharacterEntity = entity;
        });
    }

    private void LoadGltfScene(string path)
    {
        if (!File.Exists(path))
        {
            _sceneStatus = $"File not found: {path}";
            return;
        }

        ClearScene();
        _isGltfScene = true;
        SceneRenderer.ShowGroundPlane = false;
        SceneRenderer.ShowSky = true;

        bool isSponza = Path.GetFileName(path).Contains("Sponza", StringComparison.OrdinalIgnoreCase);

        // Set up initial sky, directional sun, and camera immediately so the window is live while assets stream in
        _sunEntity = World.CreateEntity("Sun Light")
            .Add(new LightComponent { Type = LightType.Directional, Color = new Vector3(1.0f, 0.96f, 0.88f), Intensity = _sunIntensityScale });
        ApplySkyAndSunLighting();

        Title = $"OpenTK-ImGui-Template — Loading {Path.GetFileName(path)}...";
        _sceneStatus = $"Loading {Path.GetFileName(path)} asynchronously...";

        int loadedAddons = 0;

        AssetLoader.LoadModelAsync(
            path,
            onLoaded: model =>
            {
                LoadedModel = model;
                World.SpawnModel(model);

                var bounds = model.Bounds;
                var center = bounds.Center;
                var size = bounds.Size;
                float diag = MathF.Max(size.Length, 1.0f);
                float floorY = isSponza ? 0.0f : bounds.Min.Y;

                SceneRenderer.ShadowFocus = new Vector3(center.X, floorY + 2.0f, center.Z);
                SceneRenderer.ShadowHalfExtent = Math.Clamp(MathF.Max(size.X, size.Z) * 0.6f, 12f, 35f);

                AddStaticFloorCollider(floorY);

                // Warm interior point lights along the main hall
                float lightY = floorY + MathF.Min(3.4f, size.Y * 0.28f);
                float lightRange = MathF.Max(14.0f, size.X * 0.55f);
                float lightIntensity = MathF.Max(16.0f, size.X * 1.0f);
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

                // Place FlyBy camera inside the atrium
                Vector3 camPos = new(center.X - size.X * 0.24f, floorY + 1.85f, center.Z + 0.4f);
                Vector3 camTarget = new(center.X + size.X * 0.08f, floorY + 1.6f, center.Z);

                _flySpeed = Math.Clamp(diag * 0.14f, 2.0f, 12.0f);
                _controlMode = 2;
                Camera.Control = new FlyByControl(camPos, camTarget)
                {
                    Speed = _flySpeed,
                    RotationSpeed = _mouseSensitivity
                };
                if (Camera.Projection is PerspectiveProjection persp)
                {
                    persp.Near = MathF.Max(0.05f, diag * 0.002f);
                    persp.Far = MathF.Max(300.0f, diag * 10.0f);
                }

                Title = $"OpenTK-ImGui-Template — {Path.GetFileName(path)}";
                _sceneStatus = $"Loaded {Path.GetFileName(path)} (+{loadedAddons} addons): {model.Primitives.Count} meshes, {model.Materials.Count} materials, {model.Textures.Count} textures";
            },
            onFailed: ex =>
            {
                _sceneStatus = $"Load failed: {ex.Message}";
            });

        // Queue Intel Sponza add-ons asynchronously in parallel!
        if (isSponza)
        {
            string? curtainsPath = FindAssetPath(
                Path.Combine("Assets", "IntelSponzaCurtains", "NewSponza_Curtains_glTF.gltf"),
                Path.Combine("Template3D", "Assets", "IntelSponzaCurtains", "NewSponza_Curtains_glTF.gltf"));
            if (curtainsPath != null)
            {
                AssetLoader.LoadModelAsync(curtainsPath, curtains =>
                {
                    _addonModels.Add(curtains);
                    World.SpawnModel(curtains, "Sponza Curtains");
                    loadedAddons++;
                    UpdateLoadedStatus(path, loadedAddons);
                });
            }

            string? ivyPath = FindAssetPath(
                Path.Combine("Assets", "IntelSponzaIvy", "NewSponza_IvyGrowth_glTF.gltf"),
                Path.Combine("Template3D", "Assets", "IntelSponzaIvy", "NewSponza_IvyGrowth_glTF.gltf"));
            if (ivyPath != null)
            {
                AssetLoader.LoadModelAsync(ivyPath, ivy =>
                {
                    _addonModels.Add(ivy);
                    World.SpawnModel(ivy, "Sponza Ivy");
                    loadedAddons++;
                    UpdateLoadedStatus(path, loadedAddons);
                });
            }

            SpawnAnimatedCharacterAsync(new Vector3(0f, 0f, 0f), targetHeight: 1.25f, yawDegrees: -90f);
            loadedAddons++;
        }
    }

    private void UpdateLoadedStatus(string mainPath, int addons)
    {
        if (LoadedModel is { } m)
            _sceneStatus = $"Loaded {Path.GetFileName(mainPath)} (+{addons} addons): {m.Primitives.Count} meshes, {m.Materials.Count} materials, {m.Textures.Count} textures";
    }

    private void BuildProceduralDemoScene()
    {
        ClearScene();
        _isGltfScene = false;
        SceneRenderer.ShowGroundPlane = true;
        SceneRenderer.ShowSky = true;
        SceneRenderer.ShadowFocus = new Vector3(0f, 1.0f, 0f);
        SceneRenderer.ShadowHalfExtent = 12f;

        // 1. Directional Sun Light (driven by Sky & Day/Night controls)
        _sunEntity = World.CreateEntity("Sun Light")
            .Add(new LightComponent { Type = LightType.Directional, Color = new Vector3(1.0f, 0.96f, 0.90f), Intensity = _sunIntensityScale });
        ApplySkyAndSunLighting();

        // 2. Clean PBR Material Showcase Row (5 spheres on a low stone plinth)
        var plinthMesh = Track(PrimitiveMeshes.CreateCube(1.0f, "Plinth"));
        var sphereMesh = Track(PrimitiveMeshes.CreateSphere(0.45f, 28, 36, "ShowcaseSphere"));

        var plinth = World.CreateEntity("Material Plinth")
            .Add(new MeshRendererComponent(plinthMesh, PbrMaterial.CreateColored("PlinthMat", new Vector3(0.26f, 0.28f, 0.31f), metallic: 0.05f, roughness: 0.78f)));
        plinth.Get<TransformComponent>().Position = new Vector3(0f, 0.1f, -2.5f);
        plinth.Get<TransformComponent>().Scale = new Vector3(7.2f, 0.2f, 1.6f);

        (string Name, Vector3 Color, float Metallic, float Roughness)[] presets =
        [
            ("Matte Slate",  new(0.32f, 0.36f, 0.42f), 0.0f, 0.85f),
            ("Ceramic Red",  new(0.82f, 0.16f, 0.18f), 0.0f, 0.25f),
            ("Brushed Gold", new(1.00f, 0.78f, 0.34f), 0.95f, 0.28f),
            ("Copper",       new(0.95f, 0.56f, 0.42f), 0.95f, 0.22f),
            ("Chrome",       new(0.92f, 0.94f, 0.96f), 1.00f, 0.08f),
        ];

        for (int i = 0; i < presets.Length; i++)
        {
            var p = presets[i];
            var orb = World.CreateEntity(p.Name)
                .Add(new MeshRendererComponent(sphereMesh, PbrMaterial.CreateColored($"{p.Name}Mat", p.Color, p.Metallic, p.Roughness)));
            orb.Get<TransformComponent>().Position = new Vector3((i - 2) * 1.4f, 0.65f, -2.5f);
        }

        // 3. BepuPhysics2 Floor + 3-2-1 Crate Pyramid
        AddStaticFloorCollider(0f);
        float baseX = 2.5f;
        float baseZ = 0.5f;
        SpawnPhysicsCrate(new Vector3(baseX - 1.05f, 0.5f, baseZ));
        SpawnPhysicsCrate(new Vector3(baseX,         0.5f, baseZ));
        SpawnPhysicsCrate(new Vector3(baseX + 1.05f, 0.5f, baseZ));
        SpawnPhysicsCrate(new Vector3(baseX - 0.52f, 1.5f, baseZ));
        SpawnPhysicsCrate(new Vector3(baseX + 0.52f, 1.5f, baseZ));
        SpawnPhysicsCrate(new Vector3(baseX,         2.5f, baseZ), new Vector3(0.85f, 0.32f, 0.22f));

        // 4. Animated glTF Fox on the left (loaded asynchronously)
        SpawnAnimatedCharacterAsync(new Vector3(-2.4f, 0f, 0.5f), targetHeight: 1.25f, yawDegrees: 30f);

        _controlMode = 3;
        Camera.Control = new EditorControl(new Vector3(0.0f, 3.8f, 8.5f), new Vector3(0.0f, 1.1f, -0.5f))
        {
            RotationSpeed = _mouseSensitivity
        };
        Title = "OpenTK-ImGui-Template — PBR & Physics Showcase";
        _sceneStatus = "PBR Material & BepuPhysics2 Showcase";
    }

    private void ApplySkyAndSunLighting()
    {
        // Map 0..24 hours -> sun elevation angle (6:00 sunrise, 12:00 zenith, 18:00 sunset)
        float hourAngle = (_timeOfDayHours - 6.0f) / 12.0f * MathF.PI;
        float elev = MathF.Sin(hourAngle);
        float horiz = MathF.Cos(hourAngle);
        float azRad = _sunAzimuthDeg * (MathF.PI / 180f);

        var sunDir = new Vector3(horiz * MathF.Cos(azRad), elev, horiz * MathF.Sin(azRad)).Normalized();

        // Day factor (0 at night, 1 at high sun) & golden-hour warmth near horizon
        float dayFactor = Math.Clamp(elev * 2.5f, 0f, 1f);
        float horizonGlow = Math.Clamp(1.0f - MathF.Abs(elev) * 3.5f, 0f, 1f) * (elev > -0.15f ? 1f : 0f);

        Vector3 noonColor = new(1.0f, 0.97f, 0.92f);
        Vector3 sunsetColor = new(1.0f, 0.52f, 0.22f);
        Vector3 nightColor = new(0.28f, 0.38f, 0.62f);

        Vector3 sunColor = Vector3.Lerp(noonColor, sunsetColor, horizonGlow);
        float effectiveIntensity = elev > 0.0f
            ? _sunIntensityScale * Math.Clamp(elev * 3.0f, 0f, 1f)
            : 0.0f;

        if (elev <= 0.0f)
            sunColor = nightColor;

        // Hemisphere ambient follows time of day (cool moonlight ambient at night)
        Vector3 skyDay = new(0.45f, 0.55f, 0.70f);
        Vector3 skySunset = new(0.55f, 0.34f, 0.28f);
        Vector3 skyNight = new(0.07f, 0.10f, 0.18f);

        Vector3 groundDay = new(0.22f, 0.19f, 0.16f);
        Vector3 groundNight = new(0.03f, 0.035f, 0.05f);

        var ambientSky = Vector3.Lerp(skyNight, Vector3.Lerp(skyDay, skySunset, horizonGlow), dayFactor);
        var ambientGround = Vector3.Lerp(groundNight, groundDay, dayFactor);

        SceneRenderer.Pbr.AmbientSky = ambientSky;
        SceneRenderer.Pbr.AmbientGround = ambientGround;

        if (_sunEntity is { IsValid: true } sun)
        {
            var light = sun.Get<LightComponent>();
            light.Color = sunColor;
            light.Intensity = effectiveIntensity;

            // Always orient TransformComponent so -UnitZ points toward the true sunDir (never flip at night)
            var tr = sun.Get<TransformComponent>();
            float pitch = MathF.Asin(Math.Clamp(sunDir.Y, -0.999f, 0.999f));
            float yaw = MathF.Atan2(-sunDir.X, -sunDir.Z);
            tr.Rotation = Quaternion.FromAxisAngle(Vector3.UnitY, yaw) * Quaternion.FromAxisAngle(Vector3.UnitX, pitch);
        }

        if (SceneRenderer.Sky is not null)
        {
            SceneRenderer.Sky.SunDir = sunDir;
            SceneRenderer.Sky.Update();
        }
    }

    private void ApplyMouseSensitivity()
    {
        switch (Camera.Control)
        {
            case FlyByControl fly: fly.RotationSpeed = _mouseSensitivity; break;
            case EditorControl ed: ed.RotationSpeed = _mouseSensitivity; break;
            case OrbitalControl orb: orb.RotationSpeed = _mouseSensitivity; break;
        }
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

    protected override void OnUnload()
    {
        ClearScene();
        AssetLoader.Dispose();
        _physicsCubeMesh.Dispose();
        _physicsSphereMesh.Dispose();
        Physics.Dispose();
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

        AssetLoader.ProcessMainThreadUploads(maxUploadsPerFrame: 2);

        if (_autoDayNightCycle)
        {
            _timeOfDayHours = (_timeOfDayHours + dt * _dayNightSpeedHoursPerSec) % 24.0f;
            if (_timeOfDayHours < 0f) _timeOfDayHours += 24.0f;
            ApplySkyAndSunLighting();
        }

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
        DrawControlPanel();
        DrawLoadingOverlay();
        FpsCounter.Render();

        if (_showDemoWindow)
            ImGui.ShowDemoWindow(ref _showDemoWindow);
    }

    private void DrawLoadingOverlay()
    {
        var jobs = AssetLoader.GetActiveProgress();
        if (jobs.Count == 0) return;

        ImGui.SetNextWindowPos(new ImVec2(ClientSize.X * 0.5f, 24), ImGuiCond.Always, new ImVec2(0.5f, 0f));
        ImGui.SetNextWindowSize(new ImVec2(420, 0), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;

        if (ImGui.Begin("##AsyncLoadingOverlay", flags))
        {
            ImGui.Text($"Loading Assets ({jobs.Count} active)...");
            ImGui.Separator();
            foreach (var job in jobs)
            {
                ImGui.Text($"{job.Label} — {job.Stage}");
                ImGui.ProgressBar(job.Fraction, new ImVec2(-1, 14));
            }
        }
        ImGui.End();
    }

    private void DrawControlPanel()
    {
        ImGui.SetNextWindowPos(new ImVec2(ClientSize.X - 325, 48), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(310, 680), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Scene, Sky & Controls"))
        {
            if (ImGui.CollapsingHeader("Scene", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Button("Intel Sponza"))
                {
                    string? sponza = FindDefaultSponzaPath();
                    if (sponza != null)
                    {
                        SetPathInput(sponza);
                        LoadGltfScene(sponza);
                    }
                    else
                    {
                        _sceneStatus = "Intel Sponza not found. Rebuild Template3D to auto-download.";
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("PBR & Physics Demo"))
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

            // Sky & Directional Sun Lighting
            if (ImGui.CollapsingHeader("Sky & Sun Lighting", ImGuiTreeNodeFlags.DefaultOpen))
            {
                int hh = (int)_timeOfDayHours;
                int mm = (int)((_timeOfDayHours - hh) * 60f);
                bool lightingChanged = false;

                ImGui.Text($"Time: {hh:D2}:{mm:D2}");
                lightingChanged |= ImGui.SliderFloat("Time of Day", ref _timeOfDayHours, 0f, 24f, "%.2f h");

                ImGui.Checkbox("Auto Day/Night Cycle", ref _autoDayNightCycle);
                if (_autoDayNightCycle)
                    ImGui.SliderFloat("Cycle Speed", ref _dayNightSpeedHoursPerSec, 0.1f, 6.0f, "%.2f h/s");

                lightingChanged |= ImGui.SliderFloat("Sun Azimuth", ref _sunAzimuthDeg, -180f, 180f, "%.0f deg");
                lightingChanged |= ImGui.SliderFloat("Sun Intensity", ref _sunIntensityScale, 0f, 8f, "%.2f");

                if (lightingChanged)
                    ApplySkyAndSunLighting();

                bool shadows = SceneRenderer.ShadowsEnabled;
                if (ImGui.Checkbox("Shadows (PCF)", ref shadows)) SceneRenderer.ShadowsEnabled = shadows;
                ImGui.SameLine();
                bool showSky = SceneRenderer.ShowSky;
                if (ImGui.Checkbox("Sky", ref showSky)) SceneRenderer.ShowSky = showSky;
                ImGui.SameLine();
                bool showGround = SceneRenderer.ShowGroundPlane;
                if (ImGui.Checkbox("Grid", ref showGround)) SceneRenderer.ShowGroundPlane = showGround;
            }

            // Character Animation Controls (always accessible!)
            if (_animatedCharacterEntity is { IsValid: true } animEntity &&
                animEntity.TryGet<AnimatorComponent>(out var anim) && anim is not null &&
                ImGui.CollapsingHeader("Character Animation", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool playing = anim.Playing;
                if (ImGui.Checkbox("Play", ref playing)) anim.Playing = playing;
                ImGui.SameLine();
                bool loop = anim.Loop;
                if (ImGui.Checkbox("Loop", ref loop)) anim.Loop = loop;

                float speed = anim.Speed;
                if (ImGui.SliderFloat("Anim Speed", ref speed, 0f, 3f, "%.2fx"))
                    anim.Speed = speed;

                if (anim.ActiveClip is { } clip)
                {
                    float t = anim.CurrentTime;
                    if (ImGui.SliderFloat("Timeline", ref t, 0f, MathF.Max(clip.Duration, 0.001f), "%.2f s"))
                        anim.CurrentTime = t;
                }

                for (int i = 0; i < anim.Clips.Count; i++)
                {
                    if (ImGui.RadioButton($"{anim.Clips[i].Name} ({anim.Clips[i].Duration:F2}s)##charclip{i}", anim.ActiveClipIndex == i))
                    {
                        anim.ActiveClipIndex = i;
                        anim.CurrentTime = 0f;
                    }
                }
            }

            // Physics (BepuPhysics2)
            if (ImGui.CollapsingHeader("Physics (BepuPhysics2)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool paused = Physics.Paused;
                if (ImGui.Checkbox("Pause Physics", ref paused))
                    Physics.Paused = paused;

                float gy = Physics.Gravity.Y;
                if (ImGui.SliderFloat("Gravity Y", ref gy, -25f, 0f, "%.2f m/s²"))
                    Physics.Gravity = new Vector3(0f, gy, 0f);

                if (ImGui.Button("+ Crate"))
                    SpawnPhysicsCrate(Camera.Position + Camera.Front * 3.5f + new Vector3(0f, 1.5f, 0f));
                ImGui.SameLine();
                if (ImGui.Button("+ Sphere"))
                    SpawnPhysicsSphere(Camera.Position + Camera.Front * 3.5f + new Vector3(0f, 1.5f, 0f));
                ImGui.SameLine();
                if (ImGui.Button("Shoot"))
                    SpawnPhysicsSphere(Camera.Position + Camera.Front * 1.4f, Camera.Front * 14f);

                ImGui.TextDisabled($"Bodies: {Physics.BodyCount}  |  Statics: {Physics.StaticCount}");
            }

            // Camera Controls
            if (ImGui.CollapsingHeader("Camera"))
            {
                using var indent = new ImGuiIndent(10f);
                if (ImGui.RadioButton("No Control", ref _controlMode, 0)) Camera.Control = new NoControl(Camera.Control);
                if (ImGui.RadioButton("Orbital", ref _controlMode, 1)) Camera.Control = new OrbitalControl(Camera.Control) { RotationSpeed = _mouseSensitivity };
                if (ImGui.RadioButton("FlyBy (WASD+Shift)", ref _controlMode, 2)) Camera.Control = new FlyByControl(Camera.Control) { Speed = _flySpeed, RotationSpeed = _mouseSensitivity };
                if (ImGui.RadioButton("Editor", ref _controlMode, 3)) Camera.Control = new EditorControl(Camera.Control) { RotationSpeed = _mouseSensitivity };

                if (ImGui.SliderFloat("Mouse Sens", ref _mouseSensitivity, 0.0005f, 0.01f, "%.4f"))
                    ApplyMouseSensitivity();

                if (Camera.Control is FlyByControl fly && ImGui.SliderFloat("Fly Speed", ref _flySpeed, 0.5f, 25f, "%.1f"))
                    fly.Speed = _flySpeed;

                if (ImGui.RadioButton("Perspective", ref _projectionMode, 0)) Camera.Projection = new PerspectiveProjection { Aspect = Camera.Aspect };
                if (ImGui.RadioButton("Orthographic", ref _projectionMode, 1)) Camera.Projection = new OrthographicProjection { Aspect = Camera.Aspect };
            }

            ImGui.Separator();
            bool vsync = VSync == VSyncMode.On;
            if (ImGui.Checkbox("VSync", ref vsync))
                VSync = vsync ? VSyncMode.On : VSyncMode.Off;
            ImGui.SameLine();
            ImGui.Checkbox("ImGui Demo", ref _showDemoWindow);
        }
        ImGui.End();
    }
}
