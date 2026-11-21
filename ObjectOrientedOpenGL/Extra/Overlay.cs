using ImSharp;

namespace ObjectOrientedOpenGL.Extra;

public class Overlay(Vector2tk position, Action draw, Anchor anchor = Anchor.TopLeft, Vector2tk? size = null)
{
    public Vector2tk Position { get; set; } = position;
    public Vector2tk? Size { get; set; } = size;
    public Action Draw { get; set; } = draw;
    public Anchor Anchor { get; set; } = anchor;
    private static uint _id;
    private string Name { get; } = $"Overlay {_id++}";

    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
                                                 ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                                                 ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse |
                                                 ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDecoration |
                                                 ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDocking |
                                                 ImGuiWindowFlags.NoFocusOnAppearing |
                                                 ImGuiWindowFlags.NoBringToFrontOnFocus;

    public void Render()
    {
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        var workPos = new Vector2tk(viewport.WorkPos.x, viewport.WorkPos.y);
        var workSize = new Vector2tk(viewport.WorkSize.x, viewport.WorkSize.y);
        var center = new Vector2tk(viewport.Pos.x + viewport.Size.x * 0.5f, viewport.Pos.y + viewport.Size.y * 0.5f);

        var (pivot, position) = Anchor switch
        {
            Anchor.TopLeft => (new ImVec2(0, 0), Position + workPos),
            Anchor.TopRight => (new ImVec2(1, 0), Position + workPos + workSize with { Y = 0 }),
            Anchor.TopCenter => (new ImVec2(0.5f, 0), Position + workPos + workSize with { X = workSize.X / 2, Y = 0 }),
            Anchor.BottomLeft => (new ImVec2(0, 1), Position + workPos + workSize with { X = 0 }),
            Anchor.BottomRight => (new ImVec2(1, 1), Position + workPos + workSize),
            Anchor.BottomCenter => (new ImVec2(0.5f, 1), Position + workPos + workSize with { X = workSize.X / 2 }),
            Anchor.CenterLeft => (new ImVec2(0, 0.5f), Position + workPos + workSize with { X = 0, Y = workSize.Y / 2 }),
            Anchor.CenterRight => (new ImVec2(1, 0.5f), Position + workPos + workSize with { Y = workSize.Y / 2 }),
            Anchor.Center => (new ImVec2(0.5f, 0.5f), Position + center),
            _ => throw new ArgumentOutOfRangeException()
        };

        ImGui.SetNextWindowPos(new ImVec2(position.X, position.Y), ImGuiCond.Always, pivot);
        if (Size is not null)
        {
            ImGui.SetNextWindowSize(new ImVec2(Size.Value.X, Size.Value.Y), ImGuiCond.Always);
        }
        ImGui.SetNextWindowBgAlpha(0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, ImVec2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ImVec2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, ImVec2.Zero);
        ImGui.Begin(Name, Size is null ? WindowFlags | ImGuiWindowFlags.AlwaysAutoResize : WindowFlags);
        Draw();
        ImGui.End();
        ImGui.PopStyleVar(4);
    }
}

public enum Anchor
{
    TopLeft,
    TopRight,
    TopCenter,
    BottomLeft,
    BottomRight,
    BottomCenter,
    CenterLeft,
    CenterRight,
    Center,
}
