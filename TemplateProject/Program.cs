using System.Runtime.InteropServices;
using ImGuiNET;
using ObjectOrientedOpenGL.Core;
using ObjectOrientedOpenGL.Extra;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ShaderType = OpenTK.Graphics.OpenGL4.ShaderType;

namespace TemplateProject;

public struct Vertex(Vector3 position, Vector2 texCoord)
{
    public Vector3 Position = position;
    public Vector2 TexCoord = texCoord;
}

public class Program(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : GameWindow(gameWindowSettings, nativeWindowSettings)
{
    private bool IsLoaded { get; set; }

    private Shader Shader { get; set; } = null!;
    private ImGuiController ImGuiController { get; set; } = null!;
    private Mesh RectangleMesh { get; set; } = null!;
    private Matrix4 ModelMatrix { get; set; }
    private Camera Camera { get; set; } = null!;
    private Plane Plane { get; set; } = null!;
    private Sky Sky { get; set; } = null!;
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

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.DebugMessageCallback(DebugProcCallback, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);

#if DEBUG
        GL.Enable(EnableCap.DebugOutputSynchronous);
#endif

        Shader = new Shader(
            ("TemplateProject.Resources.Shaders.shader.vert", ShaderType.VertexShader), 
            ("TemplateProject.Resources.Shaders.shader.frag", ShaderType.FragmentShader));
        ImGuiController = new ImGuiController(ClientSize.X, ClientSize.Y);

        Camera = new Camera(new OrbitingControl((Vector3.UnitY  + Vector3.UnitZ ) * 2, Vector3.UnitY * 2), new PerspectiveProjection());

        Vertex[] vertices = {
            new(new Vector3(0.5f, 0.5f, 0.0f), new Vector2(0.0f, 0.0f)),
            new(new Vector3(0.5f, -0.5f, 0.0f), new Vector2(0.0f, 1.0f)),
            new(new Vector3(-0.5f, -0.5f, 0.0f), new Vector2(1.0f, 1.0f)),
            new(new Vector3(-0.5f, 0.5f, 0.0f), new Vector2(1.0f, 0.0f))
        };
        int[] indices = {
            0, 1, 3,
            1, 2, 3
        };
        var indexBuffer = new IndexBuffer(indices, indices.Length * sizeof(int),
            DrawElementsType.UnsignedInt, indices.Length);
        var vertexBuffer = new VertexBuffer(vertices, vertices.Length * Marshal.SizeOf<Vertex>(),
            vertices.Length, BufferUsageHint.StaticDraw,
            new VertexBuffer.Attribute(0, 3) /*positions*/,
            new VertexBuffer.Attribute(1, 2) /*texture coords*/);
        RectangleMesh = new Mesh("Rectangle", PrimitiveType.Triangles, indexBuffer, vertexBuffer);

        ModelMatrix = Matrix4.CreateTranslation(new Vector3(0, 2, 0));

        Sky = new Sky();
        Plane = new Plane();

        Texture = new Texture("TemplateProject.Resources.Textures.texture.jpg");

        GL.ClearColor(0.4f, 0.7f, 0.9f, 1.0f);
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);

        IsLoaded = true;
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        RectangleMesh.Dispose();
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
        Sky.Update();

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

        Sky.Render(Camera);
        Plane.Render(Camera);
        
        Shader.Use();
        Texture.ActivateUnit();
        Shader.LoadInteger("sampler", 0);
        Shader.LoadMatrix4("mvp", ModelMatrix * Camera.ProjectionViewMatrix);
        RectangleMesh.Bind();
        RectangleMesh.RenderIndexed();

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

    private static int _control = 1;
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
                Camera.Control = new OrbitingControl(Camera.Control);
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
                Camera.Projection = new OrthographicProjection { Aspect = Camera.Aspect };
            ImGui.Indent(-10);
        }

        ImGui.End();
        
        Sky.ShowGui();

        ImGui.ShowDemoWindow();

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