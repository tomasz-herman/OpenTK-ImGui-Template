using System.Drawing;
using System.Runtime.InteropServices;
using ImGuiNET;
using ObjectOrientedOpenGL.Extra;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace TemplateProject;

public class Program(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : ImGuiGameWindow(gameWindowSettings, nativeWindowSettings)
{
    private Overlay Overlay { get; set; } = null!;
    private Canvas<Color, SystemColorConverter> Canvas { get; set; } = null!;
    private Vector2i MousePos { get; set; }

    private DebugProc DebugProcCallback { get; } = OnDebugMessage;

    public static void Main(string[] args)
    {
        var gwSettings = GameWindowSettings.Default;
        var nwSettings = NativeWindowSettings.Default;
        nwSettings.NumberOfSamples = 16;

#if DEBUG
        nwSettings.Flags |= ContextFlags.Debug;
#endif

        using var program = new Program(gwSettings, nwSettings);
        program.Title = "Project Title";
        program.ClientSize = new Vector2i(1280, 800);
        program.Run();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.DebugMessageCallback(DebugProcCallback, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);

#if DEBUG
        GL.Enable(EnableCap.DebugOutputSynchronous);
#endif

        Overlay = new Overlay(new Vector2(10), () => { ImGui.Text($"{MousePos.X}, {MousePos.Y}");});
        Canvas = new Canvas<Color, SystemColorConverter>(ClientSize.X, ClientSize.Y);
        
        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Disable(EnableCap.DepthTest);
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        Canvas.Dispose();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        Canvas.Resize(ClientSize.X, ClientSize.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        
        Canvas.Update();

        if (ImGui.GetIO().WantCaptureMouse) return;

        var keyboard = KeyboardState.GetSnapshot();
        var mouse = MouseState.GetSnapshot();

        if (keyboard.IsKeyDown(Keys.Escape)) Close();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        var mouse = MouseState.GetSnapshot();
        if (mouse.IsButtonDown(MouseButton.Right))
        {
            Canvas.Clear(Color.FromArgb(Random.Shared.Next()));
        }
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        var mouse = MouseState.GetSnapshot();
        MousePos = new Vector2i((int)e.X, (int)e.Y);
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Canvas.SetColor(MousePos.X, MousePos.Y, Color.White);
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        Overlay.Render();
        Canvas.Render();

        RenderGui();
        
        Context.SwapBuffers();
    }

    private static void OnDebugMessage(
        DebugSource source,     // Source of the debugging message.
        DebugType type,         // Type of the debugging message.
        int id,                 // ID associated with the message.
        DebugSeverity severity, // Severity of the message.
        int length,             // Length of the string in pMessage.
        IntPtr pMessage,        // Pointer to message string.
        IntPtr pUserParam)      // The pointer you gave to OpenGL.
    {
        var message = Marshal.PtrToStringAnsi(pMessage, length);

        var log = $"[{severity} source={source} type={type} id={id}] {message}";

        Console.WriteLine(log);
    }
}