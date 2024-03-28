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

    private Texture _fontTexture;
    private Shader _shader;
    private Mesh _mesh;
    private IndexBuffer _indexBuffer;
    private VertexBuffer _vertexBuffer;

    private int _windowWidth;
    private int _windowHeight;

    private System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;

    /// <summary>
    /// Constructs a new ImGuiController.
    /// </summary>
    public ImGuiController(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;

        IntPtr context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        CreateDeviceResources();
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

    public void DestroyDeviceObjects()
    {
        Dispose();
    }

    public void CreateDeviceResources()
    {
        _indexBuffer = new IndexBuffer(2000 * sizeof(ushort), DrawElementsType.UnsignedShort, 2000, BufferUsageHint.DynamicDraw);
        _vertexBuffer = new VertexBuffer(10000 * Marshal.SizeOf<ImDrawVert>(), 10000, BufferUsageHint.DynamicDraw,
            new Attribute(0, 2), 
            new Attribute(1, 2),
            new Attribute(2, 4, VertexAttribType.UnsignedByte, true));
        _mesh = new Mesh(PrimitiveType.Triangles, _indexBuffer, _vertexBuffer);

        RecreateFontDeviceTexture();

        _shader = new Shader(
            ("imgui.vert", ShaderType.VertexShader), 
            ("imgui.frag", ShaderType.FragmentShader));
    }

    /// <summary>
    /// Recreates the device texture used to render text.
    /// </summary>
    public void RecreateFontDeviceTexture()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height);

        _fontTexture = new Texture();
        _fontTexture.Bind();
        _fontTexture.LoadData(pixels, width, height, PixelInternalFormat.Rgba, PixelFormat.Bgra, PixelType.UnsignedByte);
        _fontTexture.ApplyOptions(Texture.Options.Default);

        io.Fonts.SetTexID((IntPtr)_fontTexture.Handle);

        io.Fonts.ClearTexData();
    }

    /// <summary>
    /// Renders the ImGui draw list data.
    /// This method requires a <see cref="GraphicsDevice"/> because it may create new DeviceBuffers if the size of vertex
    /// or index data has increased beyond the capacity of the existing buffers.
    /// A <see cref="CommandList"/> is needed to submit drawing and resource update commands.
    /// </summary>
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
            _windowWidth / _scaleFactor.X,
            _windowHeight / _scaleFactor.Y);
        io.DisplayFramebufferScale = _scaleFactor;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    readonly List<char> PressedChars = new List<char>();

    private void UpdateImGuiInput(GameWindow wnd)
    {
        ImGuiIOPtr io = ImGui.GetIO();

        MouseState MouseState = wnd.MouseState;
        KeyboardState KeyboardState = wnd.KeyboardState;

        io.MouseDown[0] = MouseState[MouseButton.Left];
        io.MouseDown[1] = MouseState[MouseButton.Right];
        io.MouseDown[2] = MouseState[MouseButton.Middle];

        var screenPoint = new Vector2i((int)MouseState.X, (int)MouseState.Y);
        var point = screenPoint;//wnd.PointToClient(screenPoint);
        io.MousePos = new System.Numerics.Vector2(point.X, point.Y);

        foreach (Keys key in Enum.GetValues(typeof(Keys)))
        {
            if (key == Keys.Unknown)
            {
                continue;
            }
            io.KeysDown[(int)key] = KeyboardState.IsKeyDown(key);
        }

        foreach (var c in PressedChars)
        {
            io.AddInputCharacter(c);
        }
        PressedChars.Clear();

        io.KeyCtrl = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);
        io.KeyAlt = KeyboardState.IsKeyDown(Keys.LeftAlt) || KeyboardState.IsKeyDown(Keys.RightAlt);
        io.KeyShift = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
        io.KeySuper = KeyboardState.IsKeyDown(Keys.LeftSuper) || KeyboardState.IsKeyDown(Keys.RightSuper);
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

    private void RenderImDrawData(ImDrawDataPtr draw_data)
    {
        if (draw_data.CmdListsCount == 0)
        {
            return;
        }

        for (int i = 0; i < draw_data.CmdListsCount; i++)
        {
            ImDrawListPtr cmd_list = draw_data.CmdListsRange[i];

            if (cmd_list.VtxBuffer.Size > _vertexBuffer.Count)
            {
                _vertexBuffer.Count *= 2;
                _vertexBuffer.Allocate(_vertexBuffer.Count * Marshal.SizeOf<ImDrawVert>());
            }

            if (cmd_list.IdxBuffer.Size > _indexBuffer.Count)
            {
                _indexBuffer.Count *= 2;
                _vertexBuffer.Allocate(_indexBuffer.Count * sizeof(ushort));
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

        _shader.Use();
        _shader.LoadMatrix4("projectionMatrix", mvp);
        _shader.LoadInteger("fontTexture", 0);

        _mesh.Bind();

        draw_data.ScaleClipRects(io.DisplayFramebufferScale);

        GL.Enable(EnableCap.Blend);
        GL.Enable(EnableCap.ScissorTest);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);

        // Render command lists
        for (int n = 0; n < draw_data.CmdListsCount; n++)
        {
            ImDrawListPtr cmd_list = draw_data.CmdListsRange[n];

            _vertexBuffer.Update(cmd_list.VtxBuffer.Data, 0, 0, Unsafe.SizeOf<ImDrawVert>() * cmd_list.VtxBuffer.Size);
            _indexBuffer.Update(cmd_list.IdxBuffer.Data, 0, 0, cmd_list.IdxBuffer.Size * sizeof(ushort));

            int vtx_offset = 0;
            int idx_offset = 0;

            for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
            {
                ImDrawCmdPtr pcmd = cmd_list.CmdBuffer[cmd_i];
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
                    GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (IntPtr)(idx_offset * sizeof(ushort)), vtx_offset);
                }
                else
                {
                    GL.DrawElements(BeginMode.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (int)pcmd.IdxOffset * sizeof(ushort));
                }

                idx_offset += (int)pcmd.ElemCount;
            }
            vtx_offset += cmd_list.VtxBuffer.Size;
        }

        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.ScissorTest);
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        _fontTexture.Dispose();
        _shader.Dispose();
    }
}