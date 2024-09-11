using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ObjectOrientedOpenGL.Extra;

public class ImGuiGameWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : GameWindow(gameWindowSettings, nativeWindowSettings)
{
    private const string FontResource = "ObjectOrientedOpenGL.Resources.Fonts.NotoSansMono.ttf";
    private bool IsLoaded { get; set; }
    private ImGuiController ImGuiController { get; set; } = null!;

    protected override void OnLoad()
    {
        base.OnLoad();
        ImGuiController = new ImGuiController(ClientSize.X, ClientSize.Y, FontResource);
        IsLoaded = true;
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        ImGuiController.Dispose();
        IsLoaded = false;
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        if (!IsLoaded) return;
        ImGuiController.OnWindowResized(ClientSize.X, ClientSize.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        ImGuiController.Update((float)args.Time);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        ImGuiController.OnKey(e, true);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        ImGuiController.OnPressedChar((char)e.Unicode);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        base.OnKeyUp(e);
        ImGuiController.OnKey(e, false);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        ImGuiController.OnMouseButton(e);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        ImGuiController.OnMouseButton(e);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        ImGuiController.OnMouseMove(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ImGuiController.OnMouseScroll(e);
    }

    protected virtual void BuildGuiLayout()
    {
        
    }

    protected void RenderGui()
    {
        BuildGuiLayout();
        ImGuiController.Render();
    }
}