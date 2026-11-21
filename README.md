# OpenTK-ImGui-Template

Game Engine Lite & 2D/3D graphics template in C# (.NET 10), powered by **OpenTK 4.9.4**, **ImSharp 1.93.0** (Dear ImGui docking + ImGuizmo + ImPlot), and **SharpGLTF**.

## Projects

- **`ObjectOrientedOpenGL`** — Core OpenGL 4.5 DSA wrappers, PBR renderer, glTF/GLB loader (`SharpGLTF`), ECS + Scene Graph (`World`, `Entity`, `TransformSystem`, `SceneRenderer`, `SceneEditorPanel`), `ImGuiGameWindow` (`ImSharp`), and `IPixelCanvas` / `PixelCanvas` / `CanvasGameWindow`.
- **`Template3D`** — 3D Game Engine Lite demo: ECS scene graph, PBR lighting (sun + orbiting point light), hierarchical transforms, in-memory SharpGLTF `.glb` loading, Scene Hierarchy, Inspector, and 3D `ImGuizmo` manipulation.
- **`Template2D`** — 2D `IPixelCanvas` (`GetPixel` / `SetPixel`) demo: Pixel Paint, Conway's Game of Life, and real-time CPU Plasma.
- **`ObjectOrientedOpenGL.Tests`** — Headless xUnit tests for ECS, Scene Graph transforms, `PixelCanvas`, and `SharpGLTF` `.glb` loading.

## Run & Test

```bash
dotnet build OpenTK-ImGui-Template.sln
dotnet test OpenTK-ImGui-Template.sln
dotnet run --project Template3D
dotnet run --project Template2D
```

## Untemplate

```bash
./untemplate.sh
```
