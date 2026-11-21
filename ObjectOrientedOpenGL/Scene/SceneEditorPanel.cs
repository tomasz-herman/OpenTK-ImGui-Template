using System.Runtime.InteropServices;
using ImSharp;
using ObjectOrientedOpenGL.Core;
using ObjectOrientedOpenGL.Graphics;
using ObjectOrientedOpenGL.Physics;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Scene;

/// <summary>
/// Built-in Game Engine Lite editor UI: Scene Hierarchy tree, Entity Inspector, and 3D ImGuizmo manipulation.
/// </summary>
public sealed class SceneEditorPanel
{
    public Entity? SelectedEntity { get; set; }
    public PhysicsWorld? Physics { get; set; }
    public ImGuizmoOperation GizmoOperation { get; set; } = ImGuizmoOperation.TRANSLATE;
    public ImGuizmoMode GizmoMode { get; set; } = ImGuizmoMode.LOCAL;
    public bool ShowGizmo { get; set; } = true;

    public bool IsGizmoUsing => ImGuizmo.IsUsing();
    public bool IsGizmoOver => ImGuizmo.IsOver();

    public void Draw(World world, Camera camera)
    {
        if (SelectedEntity is { IsValid: false })
            SelectedEntity = null;

        DrawHierarchy(world);
        DrawInspector();
        if (ShowGizmo && SelectedEntity is { IsValid: true } selected)
            DrawGizmo(selected, camera);
    }

    private void DrawHierarchy(World world)
    {
        ImGui.SetNextWindowPos(new ImVec2(16, 16), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(270, 360), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Scene Hierarchy"))
        {
            ImGui.End();
            return;
        }

        if (ImGui.Button("+ Cube"))
        {
            var e = world.CreateEntity("Cube")
                .Add(new MeshRendererComponent(PrimitiveMeshes.CreateCube(), PbrMaterial.CreateColored("CubeMat", new Vector3(0.75f, 0.45f, 0.3f), 0.1f, 0.4f)));
            e.Get<TransformComponent>().Position = new Vector3(0, 1, 0);
            SelectedEntity = e;
        }
        ImGui.SameLine();
        if (ImGui.Button("+ Sphere"))
        {
            var e = world.CreateEntity("Sphere")
                .Add(new MeshRendererComponent(PrimitiveMeshes.CreateSphere(), PbrMaterial.CreateColored("SphereMat", new Vector3(0.3f, 0.65f, 0.85f), 0.7f, 0.25f)));
            e.Get<TransformComponent>().Position = new Vector3(0, 1, 0);
            SelectedEntity = e;
        }
        ImGui.SameLine();
        if (ImGui.Button("+ Light"))
        {
            var e = world.CreateEntity("Point Light")
                .Add(new LightComponent { Type = LightType.Point, Color = new Vector3(1f, 0.85f, 0.6f), Intensity = 8f, Range = 12f });
            e.Get<TransformComponent>().Position = new Vector3(0, 2.5f, 0);
            SelectedEntity = e;
        }

        ImGui.Separator();

        foreach (var root in world.GetRootEntities())
            DrawNode(world, root);

        ImGui.End();
    }

    private void DrawNode(World world, Entity entity)
    {
        var name = entity.Get<NameComponent>();
        var hierarchy = entity.Get<HierarchyComponent>();
        bool isSelected = SelectedEntity == entity;

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (hierarchy.Children.Count <= 8) flags |= ImGuiTreeNodeFlags.DefaultOpen;
        if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (hierarchy.Children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;

        bool opened = ImGui.TreeNodeEx($"{name.Name}##{entity.Id}", flags);
        if (ImGui.IsItemClicked())
            SelectedEntity = entity;

        if (opened)
        {
            foreach (uint childId in hierarchy.Children)
                DrawNode(world, new Entity(childId, world));
            ImGui.TreePop();
        }
    }

    private void DrawInspector()
    {
        ImGui.SetNextWindowPos(new ImVec2(16, 390), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(270, 380), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Inspector"))
        {
            ImGui.End();
            return;
        }

        if (SelectedEntity is not { IsValid: true } entity)
        {
            ImGui.TextDisabled("Select an entity in Scene Hierarchy.");
            ImGui.End();
            return;
        }

        var name = entity.Get<NameComponent>();
        bool active = name.Active;
        if (ImGui.Checkbox("Active", ref active))
            name.Active = active;
        ImGui.SameLine();
        ImGui.Text($"{name.Name} (#{entity.Id})");

        ImGui.SameLine();
        if (ImGui.SmallButton("Delete"))
        {
            entity.Destroy();
            SelectedEntity = null;
            ImGui.End();
            return;
        }

        ImGui.Separator();

        // Gizmo toolbar
        bool showGizmo = ShowGizmo;
        if (ImGui.Checkbox("Gizmo", ref showGizmo)) ShowGizmo = showGizmo;
        if (ShowGizmo)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton("T", GizmoOperation == ImGuizmoOperation.TRANSLATE)) GizmoOperation = ImGuizmoOperation.TRANSLATE;
            ImGui.SameLine();
            if (ImGui.RadioButton("R", GizmoOperation == ImGuizmoOperation.ROTATE)) GizmoOperation = ImGuizmoOperation.ROTATE;
            ImGui.SameLine();
            if (ImGui.RadioButton("S", GizmoOperation == ImGuizmoOperation.SCALE)) GizmoOperation = ImGuizmoOperation.SCALE;
        }

        // Transform
        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var tr = entity.Get<TransformComponent>();
            bool changed = false;
            Span<float> pos = stackalloc float[] { tr.Position.X, tr.Position.Y, tr.Position.Z };
            if (ImGui.DragFloat3("Position", pos, 0.05f))
            {
                tr.Position = new Vector3(pos[0], pos[1], pos[2]);
                changed = true;
            }

            var euler = tr.EulerAnglesDegrees;
            Span<float> rot = stackalloc float[] { euler.X, euler.Y, euler.Z };
            if (ImGui.DragFloat3("Rotation", rot, 0.5f))
            {
                tr.EulerAnglesDegrees = new Vector3(rot[0], rot[1], rot[2]);
                changed = true;
            }

            Span<float> scl = stackalloc float[] { tr.Scale.X, tr.Scale.Y, tr.Scale.Z };
            if (ImGui.DragFloat3("Scale", scl, 0.02f))
                tr.Scale = new Vector3(scl[0], scl[1], scl[2]);

            if (changed)
                Physics?.SyncEntityToPhysics(entity);
        }

        // MeshRenderer & PBR Material
        if (entity.TryGet<MeshRendererComponent>(out var mr) && mr != null &&
            ImGui.CollapsingHeader("Mesh & PBR Material", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool vis = mr.Visible;
            if (ImGui.Checkbox("Visible", ref vis)) mr.Visible = vis;
            ImGui.Text($"Mesh: {mr.Mesh.Name}");

            var mat = mr.Material;
            Span<float> col = stackalloc float[] { mat.BaseColorFactor.X, mat.BaseColorFactor.Y, mat.BaseColorFactor.Z, mat.BaseColorFactor.W };
            if (ImGui.ColorEdit4("Base Color", col))
                mat.BaseColorFactor = new Vector4(col[0], col[1], col[2], col[3]);

            float metallic = mat.MetallicFactor;
            if (ImGui.SliderFloat("Metallic", ref metallic, 0f, 1f))
                mat.MetallicFactor = metallic;

            float roughness = mat.RoughnessFactor;
            if (ImGui.SliderFloat("Roughness", ref roughness, 0.04f, 1f))
                mat.RoughnessFactor = roughness;

            Span<float> em = stackalloc float[] { mat.EmissiveFactor.X, mat.EmissiveFactor.Y, mat.EmissiveFactor.Z };
            if (ImGui.ColorEdit3("Emissive", em))
                mat.EmissiveFactor = new Vector3(em[0], em[1], em[2]);
        }

        // Light
        if (entity.TryGet<LightComponent>(out var light) && light != null &&
            ImGui.CollapsingHeader("Light", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Text($"Type: {light.Type}");
            Span<float> col = stackalloc float[] { light.Color.X, light.Color.Y, light.Color.Z };
            if (ImGui.ColorEdit3("Color", col))
                light.Color = new Vector3(col[0], col[1], col[2]);

            float intensity = light.Intensity;
            if (ImGui.DragFloat("Intensity", ref intensity, 0.1f, 0f, 50f))
                light.Intensity = intensity;

            if (light.Type == LightType.Point)
            {
                float range = light.Range;
                if (ImGui.DragFloat("Range", ref range, 0.2f, 0.5f, 100f))
                    light.Range = range;
            }
        }

        // Rotator
        if (entity.TryGet<RotatorComponent>(out var rotator) && rotator != null &&
            ImGui.CollapsingHeader("Rotator", ImGuiTreeNodeFlags.DefaultOpen))
        {
            float spd = rotator.Speed;
            if (ImGui.DragFloat("Speed (rad/s)", ref spd, 0.05f, -10f, 10f))
                rotator.Speed = spd;
        }

        // RigidBody & Collider (BepuPhysics2)
        if (entity.TryGet<RigidBodyComponent>(out var rb) && rb != null &&
            ImGui.CollapsingHeader("RigidBody (BepuPhysics2)", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Text($"Motion: {rb.MotionType}  |  Awake: {rb.IsAwake}");
            if (rb.MotionType == BodyMotionType.Dynamic)
            {
                ImGui.Text($"Mass: {rb.Mass:F2} kg");
                ImGui.Text($"Velocity: ({rb.LinearVelocity.X:F2}, {rb.LinearVelocity.Y:F2}, {rb.LinearVelocity.Z:F2})");
                if (ImGui.Button("Impulse +Y"))
                    rb.ApplyImpulse(new Vector3(0f, rb.Mass * 6f, 0f));
            }
        }

        if (entity.TryGet<ColliderComponent>(out var colComp) && colComp != null &&
            ImGui.CollapsingHeader("Collider"))
        {
            ImGui.Text($"Shape: {colComp.Shape}");
            if (colComp.Shape == ColliderShape.Box)
                ImGui.Text($"Box Size: ({colComp.BoxSize.X:F2}, {colComp.BoxSize.Y:F2}, {colComp.BoxSize.Z:F2})");
            else if (colComp.Shape == ColliderShape.Sphere)
                ImGui.Text($"Radius: {colComp.Radius:F2}");
            else
                ImGui.Text($"Radius: {colComp.Radius:F2}, Length: {colComp.Length:F2}");
        }

        ImGui.End();
    }

    private void DrawGizmo(Entity entity, Camera camera)
    {
        var viewport = ImGui.GetMainViewport();
        ImGuizmo.SetOrthographic(camera.Projection is OrthographicProjection);
        ImGuizmo.SetDrawlist(ImGui.GetBackgroundDrawList());
        ImGuizmo.SetRect(viewport.Pos.x, viewport.Pos.y, viewport.Size.x, viewport.Size.y);

        var tr = entity.Get<TransformComponent>();
        var view = camera.ViewMatrix;
        var proj = camera.ProjectionMatrix;
        var worldMat = tr.WorldMatrix;

        Span<float> viewArr = stackalloc float[16];
        Span<float> projArr = stackalloc float[16];
        Span<float> modelArr = stackalloc float[16];
        ToSpan(view, viewArr);
        ToSpan(proj, projArr);
        ToSpan(worldMat, modelArr);

        if (ImGuizmo.Manipulate(viewArr, projArr, GizmoOperation, GizmoMode, modelArr))
        {
            var newWorld = FromSpan(modelArr);
            var h = entity.Get<HierarchyComponent>();
            if (h.ParentId.HasValue && entity.World.TryGet<TransformComponent>(h.ParentId.Value, out var parentTr) && parentTr != null)
            {
                tr.SetFromLocalMatrix(newWorld * parentTr.WorldMatrix.Inverted());
            }
            else
            {
                tr.SetFromLocalMatrix(newWorld);
            }
            Physics?.SyncEntityToPhysics(entity);
        }
    }

    private static void ToSpan(Matrix4 m, Span<float> dst) =>
        MemoryMarshal.Cast<Matrix4, float>(MemoryMarshal.CreateReadOnlySpan(ref m, 1)).CopyTo(dst);

    private static Matrix4 FromSpan(ReadOnlySpan<float> src) =>
        MemoryMarshal.Cast<float, Matrix4>(src)[0];
}
