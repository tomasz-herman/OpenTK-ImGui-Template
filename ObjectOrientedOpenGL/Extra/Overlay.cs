using System.Numerics;
using ImGuiNET;

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
        var workPos = viewport.WorkPos.ToOpenTk();
        var workSize = viewport.WorkSize.ToOpenTk();
        var (pivot, position) = Anchor switch
        {
            Anchor.TopLeft => (new Vector2(0, 0), Position + workPos),
            Anchor.TopRight => (new Vector2(1, 0), Position + workPos + workSize with { Y = 0 }),
            Anchor.TopCenter => (new Vector2(0.5f, 0), Position + workPos + workSize with { X = workSize.X / 2, Y = 0 }),
            Anchor.BottomLeft => (new Vector2(0, 1), Position + workPos + workSize with { X = 0 }),
            Anchor.BottomRight => (new Vector2(1, 1), Position + workPos + workSize),
            Anchor.BottomCenter => (new Vector2(0.5f, 1), Position + workPos + workSize with { X = workSize.X / 2 }),
            Anchor.CenterLeft => (new Vector2(0, 0.5f), Position + workPos + workSize with { X = 0, Y = workSize.Y / 2 }),
            Anchor.CenterRight => (new Vector2(1, 0.5f), Position + workPos + workSize with { Y = workSize.Y / 2 }),
            Anchor.Center => (new Vector2(0.5f), Position + viewport.GetCenter().ToOpenTk()),
            _ => throw new ArgumentOutOfRangeException()
        };

        ImGui.SetNextWindowPos(position.ToSystem(), ImGuiCond.Always, pivot);
        if (Size is not null)
        {
            ImGui.SetNextWindowSize(Size.Value.ToSystem(), ImGuiCond.Always);
        }
        ImGui.SetNextWindowBgAlpha(0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
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
