using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using TemplateProject.ImGuiUtils;
using ShaderType = OpenTK.Graphics.OpenGL4.ShaderType;

namespace TemplateProject;

public class Program : GameWindow
{
    private bool IsLoaded { get; set; }

    private Shader Shader { get; set; }
    private ImGuiController ImGuiController { get; set; }
    private Mesh RectangleMesh { get; set; }
    private Camera Camera { get; set; }
    private Texture Texture { get; set; }

    private DebugProc DebugProcCallback { get; } = OnDebugMessage;

    public static void Main(string[] args)
    {
        var gwSettings = GameWindowSettings.Default;
        var nwSettings = NativeWindowSettings.Default;
        
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

        Camera = new Camera(new FlyByControl(), new PerspectiveView());

        float[] vertices = {
            // positions
            0.5f,  0.5f, 2.0f,
            0.5f, -0.5f, 2.0f,
            -0.5f, -0.5f, 2.0f,
            -0.5f,  0.5f, 2.0f,
            // texture coords
            0.0f, 0.0f,
            0.0f, 1.0f,
            1.0f, 1.0f,
            1.0f, 0.0f
        };
        int[] indices= {
            0, 1, 3,
            1, 2, 3
        };
        var indexBuffer = new IndexBuffer(indices, sizeof(int), DrawElementsType.UnsignedInt);
        var vertexBuffer = new VertexBuffer(vertices, sizeof(float), 
            VertexBuffer.CreateSimpleLayout, 
            new Attribute(0, 3) /*positions*/,
            new Attribute(1, 2)/*texture coords*/);
        RectangleMesh = new Mesh(PrimitiveType.Triangles, indexBuffer, vertexBuffer);

        Texture = new Texture("texture.jpg");
            
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
        GL.Viewport(0, 0, Size.X, Size.Y);
        ImGuiController.WindowResized(ClientSize.X, ClientSize.Y);
        Camera.Aspect = (float) Size.X / Size.Y;
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        ImGuiController.Update(this, (float)args.Time);

        if(ImGui.GetIO().WantCaptureMouse) return;

        var keyboard = KeyboardState.GetSnapshot();
        var mouse = MouseState.GetSnapshot();
        
        Camera.HandleInput(keyboard, mouse, (float) args.Time);
            
        if (keyboard.IsKeyDown(Keys.Escape)) Close();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
            
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
        Shader.Use();
        Texture.Bind();
        Shader.LoadInteger("sampler", 0);
        Shader.LoadMatrix4("mvp", Camera.GetProjectionViewMatrix());
        RectangleMesh.Render();

        RenderGui();

        Context.SwapBuffers();
    }

    private void RenderGui()
    {
        ImGui.ShowDemoWindow();
            
        ImGuiController.Render();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
            
        ImGuiController.PressChar((char)e.Unicode);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
            
        ImGuiController.MouseScroll(e.Offset);
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