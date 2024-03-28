using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace TemplateProject;

public class ImGuiController : IDisposable
{
    private bool _frameBegun;

    private Texture FontTexture { get; }
    private Shader Shader { get; }
    private Mesh Mesh { get; }
    private IndexBuffer IndexBuffer { get; }
    private VertexBuffer VertexBuffer { get; }

    private int _windowWidth;
    private int _windowHeight;

    private System.Numerics.Vector2 ScaleFactor { get; } = System.Numerics.Vector2.One;

    public ImGuiController(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;

        FontTexture = new Texture();
        Shader = new Shader(
            ("imgui.vert", ShaderType.VertexShader),
            ("imgui.frag", ShaderType.FragmentShader));

        IndexBuffer = new IndexBuffer(2000 * sizeof(ushort), DrawElementsType.UnsignedShort, 2000, BufferUsageHint.DynamicDraw);
        VertexBuffer = new VertexBuffer(10000 * Marshal.SizeOf<ImDrawVert>(), 10000, BufferUsageHint.DynamicDraw,
            new Attribute(0, 2),
            new Attribute(1, 2),
            new Attribute(2, 4, VertexAttribType.UnsignedByte, true));
        Mesh = new Mesh(PrimitiveType.Triangles, IndexBuffer, VertexBuffer);

        IntPtr context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        RecreateFontDeviceTexture();
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        SetKeyMappings();

        SetPerFrameImGuiData(1f / 60f);

        ImGui.NewFrame();
        _frameBegun = true;
    }

    public void WindowResized(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
    }
    
    public unsafe void SetupClipboard(GameWindow wnd)
    {
        var io = ImGui.GetIO();
        io.ClipboardUserData = wnd.Context.WindowPtr;
        delegate*<Window*, byte*> getClipboard = &GLFW.GetClipboardStringRaw;
        io.GetClipboardTextFn = new IntPtr(getClipboard);
        delegate*<Window*, byte*, void> setClipboard = &GLFW.SetClipboardStringRaw;
        io.SetClipboardTextFn = new IntPtr(setClipboard);
    }

    public void RecreateFontDeviceTexture()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height);

        FontTexture.Bind();
        FontTexture.LoadData(pixels, width, height, PixelInternalFormat.Rgba, PixelFormat.Bgra, PixelType.UnsignedByte);
        FontTexture.ApplyOptions(Texture.Options.Default);

        io.Fonts.SetTexID(FontTexture.Handle);

        io.Fonts.ClearTexData();
    }

    public void Render()
    {
        if (_frameBegun)
        {
            _frameBegun = false;
            ImGui.Render();
            RenderImDrawData(ImGui.GetDrawData());
        }
    }

    /// <summary>
    /// Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(GameWindow wnd, float deltaSeconds)
    {
        if (_frameBegun)
        {
            ImGui.Render();
        }

        SetPerFrameImGuiData(deltaSeconds);
        UpdateImGuiInput(wnd);

        _frameBegun = true;
        ImGui.NewFrame();
    }

    /// <summary>
    /// Sets per-frame data based on the associated window.
    /// This is called by Update(float).
    /// </summary>
    private void SetPerFrameImGuiData(float deltaSeconds)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(
            _windowWidth / ScaleFactor.X,
            _windowHeight / ScaleFactor.Y);
        io.DisplayFramebufferScale = ScaleFactor;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    private List<char> PressedChars { get; } = new();

    private void UpdateImGuiInput(GameWindow wnd)
    {
        ImGuiIOPtr io = ImGui.GetIO();

        MouseState mouse = wnd.MouseState;
        KeyboardState keyboard = wnd.KeyboardState;

        io.MouseDown[0] = mouse[MouseButton.Left];
        io.MouseDown[1] = mouse[MouseButton.Right];
        io.MouseDown[2] = mouse[MouseButton.Middle];

        var screenPoint = new Vector2i((int)mouse.X, (int)mouse.Y);
        var point = screenPoint;//wnd.PointToClient(screenPoint);
        io.MousePos = new System.Numerics.Vector2(point.X, point.Y);

        foreach (Keys key in Enum.GetValues(typeof(Keys)))
        {
            if (key == Keys.Unknown)
            {
                continue;
            }
            io.KeysDown[(int)key] = keyboard.IsKeyDown(key);
        }

        foreach (var c in PressedChars)
        {
            io.AddInputCharacter(c);
        }
        PressedChars.Clear();

        io.KeyCtrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        io.KeyAlt = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
        io.KeyShift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        io.KeySuper = keyboard.IsKeyDown(Keys.LeftSuper) || keyboard.IsKeyDown(Keys.RightSuper);
    }

    internal void PressChar(char keyChar)
    {
        PressedChars.Add(keyChar);
    }

    internal void MouseScroll(Vector2 offset)
    {
        ImGuiIOPtr io = ImGui.GetIO();

        io.MouseWheel = offset.Y;
        io.MouseWheelH = offset.X;
    }

    private static void SetKeyMappings()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.KeyMap[(int)ImGuiKey.Tab] = (int)Keys.Tab;
        io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Keys.Left;
        io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Keys.Right;
        io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Keys.Up;
        io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Keys.Down;
        io.KeyMap[(int)ImGuiKey.PageUp] = (int)Keys.PageUp;
        io.KeyMap[(int)ImGuiKey.PageDown] = (int)Keys.PageDown;
        io.KeyMap[(int)ImGuiKey.Home] = (int)Keys.Home;
        io.KeyMap[(int)ImGuiKey.End] = (int)Keys.End;
        io.KeyMap[(int)ImGuiKey.Delete] = (int)Keys.Delete;
        io.KeyMap[(int)ImGuiKey.Backspace] = (int)Keys.Backspace;
        io.KeyMap[(int)ImGuiKey.Enter] = (int)Keys.Enter;
        io.KeyMap[(int)ImGuiKey.Escape] = (int)Keys.Escape;
        io.KeyMap[(int)ImGuiKey.A] = (int)Keys.A;
        io.KeyMap[(int)ImGuiKey.C] = (int)Keys.C;
        io.KeyMap[(int)ImGuiKey.V] = (int)Keys.V;
        io.KeyMap[(int)ImGuiKey.X] = (int)Keys.X;
        io.KeyMap[(int)ImGuiKey.Y] = (int)Keys.Y;
        io.KeyMap[(int)ImGuiKey.Z] = (int)Keys.Z;
    }

    private void RenderImDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
        {
            return;
        }

        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            ImDrawListPtr cmdList = drawData.CmdListsRange[i];

            if (cmdList.VtxBuffer.Size > VertexBuffer.Count)
            {
                VertexBuffer.Count *= 2;
                VertexBuffer.Allocate(VertexBuffer.Count * Marshal.SizeOf<ImDrawVert>());
            }

            if (cmdList.IdxBuffer.Size > IndexBuffer.Count)
            {
                IndexBuffer.Count *= 2;
                IndexBuffer.Allocate(IndexBuffer.Count * sizeof(ushort));
            }
        }

        // Setup orthographic projection matrix into our constant buffer
        ImGuiIOPtr io = ImGui.GetIO();
        Matrix4 mvp = Matrix4.CreateOrthographicOffCenter(
            0.0f,
            io.DisplaySize.X,
            io.DisplaySize.Y,
            0.0f,
            -1.0f,
            1.0f);

        Shader.Use();
        Shader.LoadMatrix4("projectionMatrix", mvp);
        Shader.LoadInteger("fontTexture", 0);

        Mesh.Bind();

        drawData.ScaleClipRects(io.DisplayFramebufferScale);

        GL.Enable(EnableCap.Blend);
        GL.Enable(EnableCap.ScissorTest);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);

        // Render command lists
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdListsRange[n];

            VertexBuffer.Update(cmdList.VtxBuffer.Data, 0, 0, Unsafe.SizeOf<ImDrawVert>() * cmdList.VtxBuffer.Size);
            IndexBuffer.Update(cmdList.IdxBuffer.Data, 0, 0, cmdList.IdxBuffer.Size * sizeof(ushort));

            int vtxOffset = 0;
            int idxOffset = 0;

            for (int cmdI = 0; cmdI < cmdList.CmdBuffer.Size; cmdI++)
            {
                ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmdI];
                if (pcmd.UserCallback != IntPtr.Zero)
                {
                    throw new NotImplementedException();
                }

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);

                // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                var clip = pcmd.ClipRect;
                GL.Scissor((int)clip.X, _windowHeight - (int)clip.W, (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));

                if ((io.BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0)
                {
                    GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, idxOffset * sizeof(ushort), vtxOffset);
                }
                else
                {
                    GL.DrawElements(BeginMode.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (int)pcmd.IdxOffset * sizeof(ushort));
                }

                idxOffset += (int)pcmd.ElemCount;
            }
            // vtxOffset += cmdList.VtxBuffer.Size;
        }

        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.ScissorTest);
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        FontTexture.Dispose();
        Shader.Dispose();
        Mesh.Dispose();
    }
}