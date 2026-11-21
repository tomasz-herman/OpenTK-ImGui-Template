using ImSharp;
using ObjectOrientedOpenGL.Core;

namespace ObjectOrientedOpenGL.Extra;

public class Billboard(Vector3tk position, Action draw)
{
    public Vector3tk Position { get; set; } = position;
    public Action Draw { get; set; } = draw;
    private static uint _id;
    private string Name { get; } = $"Billboard {_id++}";

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
            clipSpacePos /= clipSpacePos.W;

        if (clipSpacePos.Z is < 0 or > 1) return;

        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        ImVec2 viewportSize = viewport.Size;
        ImVec2 viewportPos = viewport.Pos;
        var windowPos = new ImVec2(
            viewportPos.x + (clipSpacePos.X + 1.0f) * 0.5f * viewportSize.x,
            viewportPos.y + (1.0f - clipSpacePos.Y) * 0.5f * viewportSize.y);

        if (windowPos.x < viewportPos.x || windowPos.x > viewportPos.x + viewportSize.x ||
            windowPos.y < viewportPos.y || windowPos.y > viewportPos.y + viewportSize.y)
        {
            return;
        }

        ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, new ImVec2(0.5f, 0.5f));
        ImGui.SetNextWindowBgAlpha(0.0f);
        ImGui.Begin(Name, WindowFlags);
        Draw();
        ImGui.End();
    }
}
