using ImSharp;
using ObjectOrientedOpenGL.Extra;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ObjectOrientedOpenGL.Canvas2D;

/// <summary>
/// Ready-to-use 2D application base class built around <see cref="PixelCanvas"/> and <c>ImSharp</c>.
/// </summary>
public abstract class CanvasGameWindow(
    GameWindowSettings gameWindowSettings,
    NativeWindowSettings nativeWindowSettings,
    int canvasWidth = 320,
    int canvasHeight = 200)
    : ImGuiGameWindow(gameWindowSettings, nativeWindowSettings)
{
    protected PixelCanvas Canvas { get; private set; } = null!;
    protected FpsCounter FpsCounter { get; private set; } = null!;

    /// <summary>Last hovered pixel coordinate in canvas space, or null if cursor is outside the canvas.</summary>
    protected Vector2i? HoveredPixel { get; private set; }

    protected bool IsCanvasHovered { get; private set; }

    protected override void OnLoad()
    {
        base.OnLoad();
        Canvas = new PixelCanvas(canvasWidth, canvasHeight, nearestFilter: true);
        FpsCounter = new FpsCounter();
        GL.ClearColor(0.09f, 0.10f, 0.13f, 1.0f);
        OnInitCanvas(Canvas);
    }

    protected override void OnUnload()
    {
        Canvas.Dispose();
        base.OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        FpsCounter.Update(args.Time);
        OnUpdateCanvas(Canvas, (float)args.Time);

        if (KeyboardState.IsKeyDown(Keys.Escape))
            Close();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        RenderGui();
        Context.SwapBuffers();
    }

    protected override void BuildGuiLayout()
    {
        DrawCanvasViewport();
        BuildCanvasGui();
        FpsCounter.Render();
    }

    private void DrawCanvasViewport()
    {
        ImGui.SetNextWindowPos(new ImVec2(300, 16), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(920, 740), ImGuiCond.FirstUseEver);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new ImVec2(6, 6));
        if (ImGui.Begin("2D Canvas"))
        {
            ImVec2 avail = ImGui.GetContentRegionAvail();
            float scale = MathF.Min(avail.x / Canvas.Width, avail.y / Canvas.Height);
            scale = MathF.Max(scale, 0.1f);

            var displaySize = new ImVec2(Canvas.Width * scale, Canvas.Height * scale);
            var offset = new ImVec2((avail.x - displaySize.x) * 0.5f, (avail.y - displaySize.y) * 0.5f);
            ImVec2 cursor = ImGui.GetCursorPos();
            ImGui.SetCursorPos(new ImVec2(cursor.x + MathF.Max(0, offset.x), cursor.y + MathF.Max(0, offset.y)));

            ImVec2 imgScreenPos = ImGui.GetCursorScreenPos();
            var texRef = new ImTextureRef { _TexID = (ulong)Canvas.TextureHandle };
            ImGui.Image(texRef, displaySize, ImVec2.Zero, ImVec2.One);

            IsCanvasHovered = ImGui.IsItemHovered();
            if (IsCanvasHovered)
            {
                ImVec2 mouse = ImGui.GetMousePos();
                int px = (int)((mouse.x - imgScreenPos.x) / scale);
                int py = (int)((mouse.y - imgScreenPos.y) / scale);
                HoveredPixel = Canvas.InBounds(px, py) ? new Vector2i(px, py) : null;
            }
            else
            {
                HoveredPixel = null;
            }
        }
        ImGui.End();
        ImGui.PopStyleVar();
    }

    protected virtual void OnInitCanvas(IPixelCanvas canvas) { }
    protected virtual void OnUpdateCanvas(IPixelCanvas canvas, float dt) { }
    protected virtual void BuildCanvasGui() { }
}
