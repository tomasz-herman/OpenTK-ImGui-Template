using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Canvas2D;

[StructLayout(LayoutKind.Explicit)]
public struct Pixel
{
    [FieldOffset(0)] public uint Rgba;

    [FieldOffset(0)] public byte R;
    [FieldOffset(1)] public byte G;
    [FieldOffset(2)] public byte B;
    [FieldOffset(3)] public byte A;

    public Pixel(byte r, byte g, byte b, byte a = 255)
    {
        Rgba = 0;
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static Pixel FromColor4(Color4 c) => new(
        (byte)Math.Clamp(c.R * 255f, 0f, 255f),
        (byte)Math.Clamp(c.G * 255f, 0f, 255f),
        (byte)Math.Clamp(c.B * 255f, 0f, 255f),
        (byte)Math.Clamp(c.A * 255f, 0f, 255f));

    public readonly Color4 ToColor4() => new(R / 255f, G / 255f, B / 255f, A / 255f);
}

/// <summary>
/// 2D software pixel canvas interface with <see cref="GetPixel"/> and <see cref="SetPixel"/> primitives.
/// </summary>
public interface IPixelCanvas
{
    int Width { get; }
    int Height { get; }

    Color4 GetPixel(int x, int y);
    void SetPixel(int x, int y, Color4 color);

    void Clear();
    void Fill(Color4 color);
    void DrawLine(int x0, int y0, int x1, int y1, Color4 color);
    void DrawRect(int x, int y, int width, int height, Color4 color);
    void FillRect(int x, int y, int width, int height, Color4 color);
    void DrawCircle(int cx, int cy, int radius, Color4 color);
    void FillCircle(int cx, int cy, int radius, Color4 color);
    void Resize(int width, int height, bool preserve = false);
}
