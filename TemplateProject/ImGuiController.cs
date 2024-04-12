using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace TemplateProject;

public unsafe class ImGuiController : IDisposable
{
    private bool _frameBegun;

    private Texture FontTexture { get; }
    private Shader Shader { get; }
    private Mesh Mesh { get; }
    private IndexBuffer IndexBuffer { get; }
    private VertexBuffer VertexBuffer { get; }
    private Dictionary<Keys, ImGuiKey> ModKeyMappings { get; } = new()
    {
        [Keys.LeftControl] = ImGuiKey.ModCtrl,
        [Keys.RightControl] = ImGuiKey.ModCtrl,
        [Keys.LeftShift] = ImGuiKey.ModShift,
        [Keys.RightShift] = ImGuiKey.ModShift,
        [Keys.LeftAlt] = ImGuiKey.ModAlt,
        [Keys.RightAlt] = ImGuiKey.ModAlt,
        [Keys.LeftSuper] = ImGuiKey.ModSuper,
        [Keys.RightSuper] = ImGuiKey.ModSuper,
    };
    private Dictionary<Keys, ImGuiKey> KeyMappings { get; } = new()
    {
        [Keys.Tab] = ImGuiKey.Tab,
        [Keys.Left] = ImGuiKey.LeftArrow,
        [Keys.Right] = ImGuiKey.RightArrow,
        [Keys.Up] = ImGuiKey.UpArrow,
        [Keys.Down] = ImGuiKey.DownArrow,
        [Keys.PageUp] = ImGuiKey.PageUp,
        [Keys.PageDown] = ImGuiKey.PageDown,
        [Keys.Home] = ImGuiKey.Home,
        [Keys.End] = ImGuiKey.End,
        [Keys.Insert] = ImGuiKey.Insert,
        [Keys.Delete] = ImGuiKey.Delete,
        [Keys.Backspace] = ImGuiKey.Backspace,
        [Keys.Enter] = ImGuiKey.Enter,
        [Keys.Escape] = ImGuiKey.Escape,
        [Keys.LeftControl] = ImGuiKey.LeftCtrl,
        [Keys.RightControl] = ImGuiKey.RightCtrl,
        [Keys.LeftShift] = ImGuiKey.LeftShift,
        [Keys.RightShift] = ImGuiKey.RightShift,
        [Keys.LeftAlt] = ImGuiKey.LeftAlt,
        [Keys.RightAlt] = ImGuiKey.RightAlt,
        [Keys.LeftSuper] = ImGuiKey.LeftSuper,
        [Keys.RightSuper] = ImGuiKey.RightSuper,
        [Keys.Apostrophe] = ImGuiKey.Apostrophe,
        [Keys.Comma] = ImGuiKey.Comma,
        [Keys.Minus] = ImGuiKey.Minus,
        [Keys.Period] = ImGuiKey.Period,
        [Keys.Slash] = ImGuiKey.Slash,
        [Keys.Semicolon] = ImGuiKey.Semicolon,
        [Keys.Equal] = ImGuiKey.Equal,
        [Keys.LeftBracket] = ImGuiKey.LeftBracket,
        [Keys.Backslash] = ImGuiKey.Backslash,
        [Keys.RightBracket] = ImGuiKey.RightBracket,
        [Keys.GraveAccent] = ImGuiKey.GraveAccent,
        [Keys.CapsLock] = ImGuiKey.CapsLock,
        [Keys.ScrollLock] = ImGuiKey.ScrollLock,
        [Keys.NumLock] = ImGuiKey.NumLock,
        [Keys.PrintScreen] = ImGuiKey.PrintScreen,
        [Keys.Pause] = ImGuiKey.Pause,
        [Keys.KeyPad0] = ImGuiKey.Keypad0,
        [Keys.KeyPad1] = ImGuiKey.Keypad1,
        [Keys.KeyPad2] = ImGuiKey.Keypad2,
        [Keys.KeyPad3] = ImGuiKey.Keypad3,
        [Keys.KeyPad4] = ImGuiKey.Keypad4,
        [Keys.KeyPad5] = ImGuiKey.Keypad5,
        [Keys.KeyPad6] = ImGuiKey.Keypad6,
        [Keys.KeyPad7] = ImGuiKey.Keypad7,
        [Keys.KeyPad8] = ImGuiKey.Keypad8,
        [Keys.KeyPad9] = ImGuiKey.Keypad9,
        [Keys.KeyPadDecimal] = ImGuiKey.KeypadDecimal,
        [Keys.KeyPadDivide] = ImGuiKey.KeypadDivide,
        [Keys.KeyPadMultiply] = ImGuiKey.KeypadMultiply,
        [Keys.KeyPadSubtract] = ImGuiKey.KeypadSubtract,
        [Keys.KeyPadAdd] = ImGuiKey.KeypadAdd,
        [Keys.KeyPadEnter] = ImGuiKey.KeypadEnter,
        [Keys.KeyPadEqual] = ImGuiKey.KeypadEqual,
        [Keys.Menu] = ImGuiKey.Menu,
        [Keys.D0] = ImGuiKey._0,
        [Keys.D1] = ImGuiKey._1,
        [Keys.D2] = ImGuiKey._2,
        [Keys.D3] = ImGuiKey._3,
        [Keys.D4] = ImGuiKey._4,
        [Keys.D5] = ImGuiKey._5,
        [Keys.D6] = ImGuiKey._6,
        [Keys.D7] = ImGuiKey._7,
        [Keys.D8] = ImGuiKey._8,
        [Keys.D9] = ImGuiKey._9,
        [Keys.A] = ImGuiKey.A,
        [Keys.B] = ImGuiKey.B,
        [Keys.C] = ImGuiKey.C,
        [Keys.D] = ImGuiKey.D,
        [Keys.E] = ImGuiKey.E,
        [Keys.F] = ImGuiKey.F,
        [Keys.G] = ImGuiKey.G,
        [Keys.H] = ImGuiKey.H,
        [Keys.I] = ImGuiKey.I,
        [Keys.J] = ImGuiKey.J,
        [Keys.K] = ImGuiKey.K,
        [Keys.L] = ImGuiKey.L,
        [Keys.M] = ImGuiKey.M,
        [Keys.N] = ImGuiKey.N,
        [Keys.O] = ImGuiKey.O,
        [Keys.P] = ImGuiKey.P,
        [Keys.Q] = ImGuiKey.Q,
        [Keys.R] = ImGuiKey.R,
        [Keys.S] = ImGuiKey.S,
        [Keys.T] = ImGuiKey.T,
        [Keys.U] = ImGuiKey.U,
        [Keys.V] = ImGuiKey.V,
        [Keys.W] = ImGuiKey.W,
        [Keys.X] = ImGuiKey.X,
        [Keys.Y] = ImGuiKey.Y,
        [Keys.Z] = ImGuiKey.Z,
        [Keys.F1] = ImGuiKey.F1,
        [Keys.F2] = ImGuiKey.F2,
        [Keys.F3] = ImGuiKey.F3,
        [Keys.F4] = ImGuiKey.F4,
        [Keys.F5] = ImGuiKey.F5,
        [Keys.F6] = ImGuiKey.F6,
        [Keys.F7] = ImGuiKey.F7,
        [Keys.F8] = ImGuiKey.F8,
        [Keys.F9] = ImGuiKey.F9,
        [Keys.F10] = ImGuiKey.F10,
        [Keys.F11] = ImGuiKey.F11,
        [Keys.F12] = ImGuiKey.F12,
        [Keys.F13] = ImGuiKey.F13,
        [Keys.F14] = ImGuiKey.F14,
        [Keys.F15] = ImGuiKey.F15,
        [Keys.F16] = ImGuiKey.F16,
        [Keys.F17] = ImGuiKey.F17,
        [Keys.F18] = ImGuiKey.F18,
        [Keys.F19] = ImGuiKey.F19,
        [Keys.F20] = ImGuiKey.F20,
        [Keys.F21] = ImGuiKey.F21,
        [Keys.F22] = ImGuiKey.F22,
        [Keys.F23] = ImGuiKey.F23,
        [Keys.F24] = ImGuiKey.F24
    };

    private State State { get; } = new(
        new State.Texture2D(),
        new State.Program(),
        new State.Sampler(0),
        new State.ArrayBuffer(),
        new State.VertexArrayObject(),
        new State.ElementArrayBuffer(),
        new State.PolygonMode(),
        new State.Blend(),
        new State.Scissor(),
        new State.Capability(EnableCap.CullFace),
        new State.Capability(EnableCap.DepthTest),
        new State.Capability(EnableCap.StencilTest)
    );

    private int _windowWidth;
    private int _windowHeight;

    private System.Numerics.Vector2 ScaleFactor { get; } = System.Numerics.Vector2.One;

    public ImGuiController(int width, int height)
    {
        using var prevState = State.Save();
        _windowWidth = width;
        _windowHeight = height;

        FontTexture = new Texture();
        Shader = new Shader(
            ("imgui.vert", ShaderType.VertexShader),
            ("imgui.frag", ShaderType.FragmentShader));

        IndexBuffer = new IndexBuffer(2000 * sizeof(ushort), DrawElementsType.UnsignedShort, 2000, BufferUsageHint.DynamicDraw);
        VertexBuffer = new VertexBuffer(10000 * sizeof(ImDrawVert), 10000, BufferUsageHint.DynamicDraw,
            new VertexBuffer.Attribute(0, 2),
            new VertexBuffer.Attribute(1, 2),
            new VertexBuffer.Attribute(2, 4, VertexAttribType.UnsignedByte, true));
        Mesh = new Mesh(PrimitiveType.Triangles, IndexBuffer, VertexBuffer);

        CreateContext();
    }

    private void CreateContext()
    {
        IntPtr context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        RecreateFontDeviceTexture();
        SetupClipboard();
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    }

    public void OnWindowResized(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
    }

    private void SetupClipboard()
    {
        var io = ImGui.GetIO();
        io.ClipboardUserData = new IntPtr(GLFW.GetCurrentContext());
        delegate*<Window*, byte*> getClipboard = &GLFW.GetClipboardStringRaw;
        io.GetClipboardTextFn = new IntPtr(getClipboard);
        delegate*<Window*, byte*, void> setClipboard = &GLFW.SetClipboardStringRaw;
        io.SetClipboardTextFn = new IntPtr(setClipboard);
    }

    private void RecreateFontDeviceTexture()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height);

        FontTexture.Bind();
        FontTexture.LoadData(pixels, width, height, PixelInternalFormat.Rgba, PixelFormat.Bgra, PixelType.UnsignedByte);
        FontTexture.ApplyOptions(
            Texture.Options.Default
                .SetParameter(TextureParameterName.TextureMinFilter, TextureMinFilter.Linear));

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

    public void Update(float dt)
    {
        if (_frameBegun)
        {
            ImGui.Render();
        }

        SetPerFrameImGuiData(dt);

        _frameBegun = true;
        ImGui.NewFrame();
    }

    private void SetPerFrameImGuiData(float deltaSeconds)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(
            _windowWidth / ScaleFactor.X,
            _windowHeight / ScaleFactor.Y);
        io.DisplayFramebufferScale = ScaleFactor;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    public void OnMouseMove(MouseMoveEventArgs e)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddMousePosEvent(e.X, e.Y);
    }

    public void OnMouseButton(MouseButtonEventArgs e)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        var button = (int)e.Button;
        if (button is >= 0 and < (int)ImGuiMouseButton.COUNT)
            io.AddMouseButtonEvent(button, e.Action == InputAction.Press);
    }

    public void OnKey(KeyboardKeyEventArgs e, bool down)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        if (KeyMappings.TryGetValue(e.Key, out var key))
        {
            io.AddKeyEvent(key, down);
        }
        if (ModKeyMappings.TryGetValue(e.Key, out var modKey))
        {
            io.AddKeyEvent(modKey, down);
        }
    }

    public void OnPressedChar(char keyChar)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddInputCharacter(keyChar);
    }

    public void OnMouseScroll(MouseWheelEventArgs e)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddMouseWheelEvent(e.OffsetX, e.OffsetY);
    }

    private void RenderImDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
        {
            return;
        }

        using var prevState = State.Save();

        if (drawData.TotalVtxCount > VertexBuffer.Count)
        {
            VertexBuffer.Count = drawData.TotalVtxCount * 2;
            VertexBuffer.Allocate(VertexBuffer.Count * sizeof(ImDrawVert));
        }

        if (drawData.TotalIdxCount > IndexBuffer.Count)
        {
            IndexBuffer.Count = drawData.TotalIdxCount * 2;
            IndexBuffer.Allocate(IndexBuffer.Count * sizeof(ushort));
        }

        int vtxOffset = 0;
        int idxOffset = 0;
        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            var cmdList = drawData.CmdLists[i];

            VertexBuffer.Update(cmdList.VtxBuffer.Data, 0, vtxOffset * sizeof(ImDrawVert), cmdList.VtxBuffer.Size * sizeof(ImDrawVert));
            IndexBuffer.Update(cmdList.IdxBuffer.Data, 0, idxOffset * sizeof(ushort), cmdList.IdxBuffer.Size * sizeof(ushort));

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
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
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha,
            BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

        // Render command lists
        vtxOffset = 0;
        idxOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];

            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                ImDrawCmdPtr cmd = cmdList.CmdBuffer[i];
                if (cmd.UserCallback != IntPtr.Zero)
                {
                    throw new NotImplementedException();
                }

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, (int)cmd.TextureId);

                // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                var clip = cmd.ClipRect;
                GL.Scissor((int)clip.X, _windowHeight - (int)clip.W, (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));

                Mesh.RenderIndexed((int)(cmd.IdxOffset + idxOffset) * sizeof(ushort), (int)cmd.ElemCount, (int)(cmd.VtxOffset + vtxOffset));
            }

            idxOffset += cmdList.IdxBuffer.Size;
            vtxOffset += cmdList.VtxBuffer.Size;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        FontTexture.Dispose();
        Shader.Dispose();
        Mesh.Dispose();
        ImGui.DestroyContext();
    }
}