﻿using System.Runtime.CompilerServices;
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
    private Dictionary<Keys, ImGuiKey> KeyMappings { get; } = new (){
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
        [Keys.LeftControl] = ImGuiKey.ModCtrl,
        [Keys.RightControl] = ImGuiKey.ModCtrl,
        [Keys.LeftShift] = ImGuiKey.ModShift,
        [Keys.RightShift] = ImGuiKey.ModShift,
        [Keys.LeftAlt] = ImGuiKey.ModAlt,
        [Keys.RightAlt] = ImGuiKey.ModAlt,
        [Keys.LeftSuper] = ImGuiKey.ModSuper,
        [Keys.RightSuper] = ImGuiKey.ModSuper,
        [Keys.A] = ImGuiKey.A,
        [Keys.C] = ImGuiKey.C,
        [Keys.V] = ImGuiKey.V,
        [Keys.X] = ImGuiKey.X,
        [Keys.Y] = ImGuiKey.Y,
        [Keys.Z] = ImGuiKey.Z
    };

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

        CreateContext();
    }

    private void CreateContext()
    {
        IntPtr context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        RecreateFontDeviceTexture();
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
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
        io.MousePos = new System.Numerics.Vector2(point.X / ScaleFactor.X, point.Y / ScaleFactor.Y);

        foreach (var (key, imKey) in KeyMappings)
        {
            if(!keyboard.WasKeyDown(key) && keyboard.IsKeyDown(key))
            {
                io.AddKeyEvent(imKey, true);
            }
            else if(keyboard.WasKeyDown(key) && !keyboard.IsKeyDown(key))
            {
                io.AddKeyEvent(imKey, false);
            }
        }

        foreach (var c in PressedChars)
        {
            io.AddInputCharacter(c);
        }
        PressedChars.Clear();
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

    private void RenderImDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
        {
            return;
        }

        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[i];

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
            ImDrawListPtr cmdList = drawData.CmdLists[n];

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
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        FontTexture.Dispose();
        Shader.Dispose();
        Mesh.Dispose();
        ImGui.DestroyContext();
    }
}