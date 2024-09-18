using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using ObjectOrientedOpenGL.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Vector2 = System.Numerics.Vector2;

namespace ObjectOrientedOpenGL.Extra;

public class Canvas<TColor, TColorConverter> : IDisposable 
    where TColorConverter : IColorConverter<TColor> 
    where TColor : struct
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

    public TColor GetColor(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return default;
        }
        
        return TColorConverter.PixelToColor(_data[y, x]);
    }
    
    public void SetColor(int x, int y, TColor color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        _data[y, x] = TColorConverter.ColorToPixel(color);
    }

    public void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        _data = new Pixel[height, width];
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
}

[StructLayout(LayoutKind.Explicit)]
public struct Pixel
{
    [FieldOffset(0)] public uint Rgba;

    [FieldOffset(0)] public byte R;
    [FieldOffset(1)] public byte G;
    [FieldOffset(2)] public byte B;
    [FieldOffset(3)] public byte A;
}

public interface IColorConverter<TColor>
    where TColor : struct
{
    static abstract Pixel ColorToPixel(TColor color);
    static abstract TColor PixelToColor(Pixel pixel);
}

public abstract class OpenTkColor4Converter : IColorConverter<Color4>
{
    public static Pixel ColorToPixel(Color4 color)
    {
        return new Pixel
        {
            R = (byte)(color.R * byte.MaxValue),
            G = (byte)(color.G * byte.MaxValue),
            B = (byte)(color.B * byte.MaxValue),
            A = (byte)(color.A * byte.MaxValue)
        };
    }

    public static Color4 PixelToColor(Pixel pixel)
    {
        return new Color4(pixel.R, pixel.G, pixel.B, pixel.A);
    }
}

public abstract class SystemColorConverter : IColorConverter<Color>
{
    public static Pixel ColorToPixel(Color color)
    {
        return new Pixel { R = color.R, G = color.G, B = color.B, A = color.A };
    }

    public static Color PixelToColor(Pixel pixel)
    {
        return Color.FromArgb((int)BitOperations.RotateRight(pixel.Rgba, 8));
    }
}

