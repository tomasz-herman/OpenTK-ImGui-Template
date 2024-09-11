using System.Runtime.InteropServices;
using ImGuiNET;
using ObjectOrientedOpenGL.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Vector2 = System.Numerics.Vector2;

namespace ObjectOrientedOpenGL.Extra;

public class Canvas : IDisposable
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    private Texture? Texture { get; set; }
    private Overlay Overlay { get; }

    private Pixel[,] _data = null!;

    public Canvas(int width, int height)
    {
        Overlay = new Overlay(Vector2tk.Zero,
            () => ImGui.Image(new IntPtr(Texture?.Handle ?? 0), new Vector2(Width, Height)));
        Resize(width, height);
    }

    public Color4 GetColor(int x, int y)
    {
        ref var pixel = ref _data[x, y];
        return new Color4(pixel.r, pixel.g, pixel.b, pixel.a);
    }
    
    public void SetColor(int x, int y, Color4 color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }
        ref var pixel = ref _data[x, y];
        pixel.r = (byte)(color.R * byte.MaxValue);
        pixel.g = (byte)(color.G * byte.MaxValue);
        pixel.b = (byte)(color.B * byte.MaxValue);
        pixel.a = (byte)(color.A * byte.MaxValue);
    }

    public void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        _data = new Pixel[width, height];
        Texture?.Dispose();
        Texture = new Texture();
        Texture.Allocate(width, height, SizedInternalFormat.Rgba8, 1);
        Overlay.Size = new Vector2tk(width, height);
        Update();
    }

    public void Update()
    {
        Texture!.Update(_data, 0, 0, Width, Height, PixelFormat.Rgba, PixelType.UnsignedByte);
    }
    
    public void Render()
    {
        Overlay.Render();
    }

    public void Dispose()
    {
        Texture!.Dispose();
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct Pixel
    {
        [FieldOffset(0)] public uint rgba;

        [FieldOffset(0)] public byte r;
        [FieldOffset(1)] public byte g;
        [FieldOffset(2)] public byte b;
        [FieldOffset(3)] public byte a;
    }
}