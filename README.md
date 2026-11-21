# OpenTK-ImGui-Template

Game Engine Lite & 2D/3D graphics template in C# (.NET 10), powered by **OpenTK 4.9.4**, **ImSharp 1.93.0** (Dear ImGui docking + ImGuizmo + ImPlot), **SharpGLTF**, and **BepuPhysics2**.

## Projects

- **`ObjectOrientedOpenGL`** — Core OpenGL 4.5 DSA wrappers, PBR renderer, glTF/GLB loader (`SharpGLTF`), ECS + Scene Graph (`World`, `Entity`, `TransformSystem`, `SceneRenderer`, `SceneEditorPanel`), **BepuPhysics2** (`PhysicsWorld`, `RigidBodyComponent`, `ColliderComponent`, raycasting), `ImGuiGameWindow` (`ImSharp`), and `IPixelCanvas` / `PixelCanvas` / `CanvasGameWindow`.
- **`Template3D`** — 3D Game Engine Lite demo: ECS scene graph, PBR lighting, BepuPhysics2 rigid body simulation (crates, spheres, raycasting), glTF 2.0 / Intel Sponza loader, Scene Hierarchy, Inspector, and 3D `ImGuizmo` manipulation.
- **`Template2D`** — 2D `IPixelCanvas` (`GetPixel` / `SetPixel`) demo: Pixel Paint, Conway's Game of Life, and real-time CPU Plasma.
- **`ObjectOrientedOpenGL.Tests`** — Headless xUnit tests for ECS, Scene Graph transforms, BepuPhysics2 simulation & raycasting, `PixelCanvas`, and `SharpGLTF` loading.

## Run & Test

```bash
dotnet build OpenTK-ImGui-Template.sln # auto-downloads Intel Sponza glTF on first build (-p:DownloadIntelSponza=false to skip)
dotnet test OpenTK-ImGui-Template.sln
dotnet run --project Template3D
dotnet run --project Template2D
```

## Untemplate

```bash
./untemplate.sh
```
