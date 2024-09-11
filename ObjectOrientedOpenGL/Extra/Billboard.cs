using System.Numerics;
using ImGuiNET;
using ObjectOrientedOpenGL.Core;

namespace ObjectOrientedOpenGL.Extra;

public class Billboard(Vector3tk position, Action draw)
{
    public Vector3tk Position { get; set; } = position;
    public Action Draw { get; set; } = draw;
    private static uint _id;
    private string Name { get; } = $"Billbaord {_id++}";

    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
                                                 ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                                                 ImGuiWindowFlags.NoScrollbar |
                                                 ImGuiWindowFlags.NoCollapse |
                                                 ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize |
                                                 ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs |
                                                 ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing |
                                                 ImGuiWindowFlags.NoBringToFrontOnFocus;

    public void Render(Camera camera)
    {
        Vector4tk clipSpacePos = new Vector4tk(Position, 1.0f) * camera.ProjectionViewMatrix;
        if (clipSpacePos.W != 0)
        {
            clipSpacePos /= clipSpacePos.W;
        }
        if (clipSpacePos.Z is < 0 or > 1) return;
        
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        Vector2tk viewportSize = viewport.Size.ToOpenTk();
        Vector2tk windowPos = Vector2tk.Zero;
        windowPos.X = (clipSpacePos.X + 1.0f) * 0.5f * viewportSize.X;
        windowPos.Y = (1.0f - clipSpacePos.Y) * 0.5f * viewportSize.Y;
        
        if (OutOfBounds(viewportSize, windowPos))
        {
            return;
        }

        ImGui.SetNextWindowPos(windowPos.ToSystem(), ImGuiCond.Always, new Vector2(0.5f));
        ImGui.SetNextWindowBgAlpha(0.0f);
        ImGui.Begin(Name, WindowFlags);
        Draw();
        ImGui.End();
    }

    public bool OutOfBounds(Vector2tk windowSize, Vector2tk windowPos)
    {
        bool fitsHorizontally = windowPos.X >= 0 && windowPos.X <= windowSize.X;
        bool fitsVertically = windowPos.Y >= 0 && windowPos.Y <= windowSize.Y;

        return !(fitsHorizontally && fitsVertically);
    }
}