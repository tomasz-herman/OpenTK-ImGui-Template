using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ShaderType = OpenTK.Graphics.OpenGL4.ShaderType;
using Vector4 = System.Numerics.Vector4;

namespace TemplateProject;

public class Program : GameWindow
{
    private bool IsLoaded { get; set; }

    private Shader Shader { get; set; } = null!;
    private ImGuiController ImGuiController { get; set; } = null!;
    private WireframeCube WireframeCube { get; set; } = null!;
    private Camera Camera { get; set; } = null!;
    private Texture Texture { get; set; } = null!;

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
        program.Size = new Vector2i(1280, 800);
        program.Run();
    }

    public Program(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings) { }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.DebugMessageCallback(DebugProcCallback, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);

#if DEBUG
        GL.Enable(EnableCap.DebugOutputSynchronous);
#endif

        Shader = new Shader(("shader.vert", ShaderType.VertexShader), ("shader.frag", ShaderType.FragmentShader));
        ImGuiController = new ImGuiController(ClientSize.X, ClientSize.Y);

        Camera = new Camera(new NoControl(5 * Vector3.UnitZ, Vector3.Zero), new PerspectiveProjection());

        WireframeCube = new WireframeCube();

        Texture = new Texture("texture.jpg");

        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);

        IsLoaded = true;
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        WireframeCube.Dispose();
        ImGuiController.Dispose();
        Texture.Dispose();
        Shader.Dispose();

        IsLoaded = false;
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        if (!IsLoaded) return;

        base.OnResize(e);
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        ImGuiController.OnWindowResized(ClientSize.X, ClientSize.Y);
        Camera.Aspect = (float)ClientSize.X / ClientSize.Y;
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        ImGuiController.Update((float)args.Time);
        Camera.Update((float)args.Time);

        WireframeCube.ModelMatrix *= Matrix4.CreateRotationY((float)args.Time * 0.1f);

        if (ImGui.GetIO().WantCaptureMouse) return;

        var keyboard = KeyboardState.GetSnapshot();
        var mouse = MouseState.GetSnapshot();

        Camera.HandleInput((float)args.Time, keyboard, mouse);

        if (keyboard.IsKeyDown(Keys.Escape)) Close();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Shader.Use();
        Matrix4 mvp = WireframeCube.ModelMatrix * Camera.ProjectionViewMatrix;
        Shader.LoadMatrix4("mvp", WireframeCube.ModelMatrix * Camera.ProjectionViewMatrix);
        WireframeCube.Mesh.Bind();
        WireframeCube.Mesh.RenderIndexed();

        DebugMatrix(WireframeCube.ModelMatrix, "Model Matrix");
        DebugMatrix(Camera.ViewMatrix, "View Matrix");
        DebugMatrix(Camera.ProjectionMatrix, "Projection Matrix");
        DebugMatrix(mvp, "MVP Matrix");

        RenderGui();

        Context.SwapBuffers();
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);

        ImGuiController.OnKey(e, true);
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

    private void DebugMatrix(Matrix4 matrix, string name)
    {
        ImGui.Begin(name, ImGuiWindowFlags.AlwaysAutoResize);
        var c0 = new Vector4(matrix.Column0.X, matrix.Column0.Y, matrix.Column0.Z, matrix.Column0.W);
        var c1 = new Vector4(matrix.Column1.X, matrix.Column1.Y, matrix.Column1.Z, matrix.Column1.W);
        var c2 = new Vector4(matrix.Column2.X, matrix.Column2.Y, matrix.Column2.Z, matrix.Column2.W);
        var c3 = new Vector4(matrix.Column3.X, matrix.Column3.Y, matrix.Column3.Z, matrix.Column3.W);
        ImGui.PushID($"{name}_c0"); ImGui.InputFloat4("", ref c0); ImGui.PopID();
        ImGui.PushID($"{name}_c1"); ImGui.InputFloat4("", ref c1); ImGui.PopID();
        ImGui.PushID($"{name}_c2"); ImGui.InputFloat4("", ref c2); ImGui.PopID();
        ImGui.PushID($"{name}_c3"); ImGui.InputFloat4("", ref c3); ImGui.PopID();
        ImGui.End();
    }

    private static int _control;
    private static int _projection;
    private void RenderGui()
    {
        ImGui.Begin("Camera");
        if (ImGui.CollapsingHeader("Control"))
        {
            ImGui.Indent(10);
            if (ImGui.RadioButton("No Control", ref _control, 0))
                Camera.Control = new NoControl(Camera.Control);
            if (ImGui.RadioButton("Orbital Control", ref _control, 1))
                Camera.Control = new OrbitingControl(Camera.Position, Vector3.Zero);
            if (ImGui.RadioButton("FlyBy Control", ref _control, 2))
                Camera.Control = new FlyByControl(Camera.Control);

            ImGui.Indent(-10);
        }

        if (ImGui.CollapsingHeader("Projection"))
        {
            ImGui.Indent(10);
            if (ImGui.RadioButton("Perspective", ref _projection, 0))
                Camera.Projection = new PerspectiveProjection { Aspect = Camera.Aspect };
            if (ImGui.RadioButton("Orthographic", ref _projection, 1))
                Camera.Projection = new OrthographicProjection { Aspect = Camera.Aspect, Height = 5 };
            ImGui.Indent(-10);
        }

        ImGui.End();

        ImGuiController.Render();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        ImGuiController.OnPressedChar((char)e.Unicode);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        ImGuiController.OnMouseScroll(e);
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