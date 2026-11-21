using ObjectOrientedOpenGL.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Canvas2D;

/// <summary>
/// RGBA8 2D pixel canvas implementing <see cref="IPixelCanvas"/> with lazy OpenGL texture upload.
/// </summary>
public sealed class PixelCanvas : IPixelCanvas, IDisposable
{
    private Pixel[] _pixels = [];
    private Texture? _texture;
    private bool _dirty = true;
    private bool _nearestFilter = true;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public ReadOnlySpan<Pixel> RawPixels => _pixels;

    public bool NearestFilter
    {
        get => _nearestFilter;
        set
        {
            if (_nearestFilter == value) return;
            _nearestFilter = value;
            _texture?.ApplyOptions(_nearestFilter ? Texture.Options.Nearest : Texture.Options.Linear);
        }
    }

    public int TextureHandle
    {
        get
        {
            Upload();
            return _texture?.Handle ?? 0;
        }
    }

    public PixelCanvas(int width, int height, bool nearestFilter = true)
    {
        _nearestFilter = nearestFilter;
        Resize(width, height);
    }

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Color4 GetPixel(int x, int y) =>
        InBounds(x, y) ? _pixels[y * Width + x].ToColor4() : default;

    public void SetPixel(int x, int y, Color4 color)
    {
        if (!InBounds(x, y)) return;
        _pixels[y * Width + x] = Pixel.FromColor4(color);
        _dirty = true;
    }

    public Pixel GetRawPixel(int x, int y) =>
        InBounds(x, y) ? _pixels[y * Width + x] : default;

    public void SetRawPixel(int x, int y, Pixel pixel)
    {
        if (!InBounds(x, y)) return;
        _pixels[y * Width + x] = pixel;
        _dirty = true;
    }

    public void Clear()
    {
        Array.Clear(_pixels);
        _dirty = true;
    }

    public void Fill(Color4 color)
    {
        Array.Fill(_pixels, Pixel.FromColor4(color));
        _dirty = true;
    }

    public void DrawLine(int x0, int y0, int x1, int y1, Color4 color)
    {
        var px = Pixel.FromColor4(color);
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            if (InBounds(x0, y0))
                _pixels[y0 * Width + x0] = px;

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
        _dirty = true;
    }

    public void DrawRect(int x, int y, int width, int height, Color4 color)
    {
        if (width <= 0 || height <= 0) return;
        int x2 = x + width - 1;
        int y2 = y + height - 1;
        DrawLine(x, y, x2, y, color);
        DrawLine(x, y2, x2, y2, color);
        DrawLine(x, y, x, y2, color);
        DrawLine(x2, y, x2, y2, color);
    }

    public void FillRect(int x, int y, int width, int height, Color4 color)
    {
        var px = Pixel.FromColor4(color);
        int minX = Math.Max(0, x);
        int maxX = Math.Min(Width, x + width);
        int minY = Math.Max(0, y);
        int maxY = Math.Min(Height, y + height);
        for (int py = minY; py < maxY; py++)
        {
            int row = py * Width;
            for (int pxX = minX; pxX < maxX; pxX++)
                _pixels[row + pxX] = px;
        }
        _dirty = true;
    }

    public void DrawCircle(int cx, int cy, int radius, Color4 color)
    {
        var px = Pixel.FromColor4(color);
        int x = radius;
        int y = 0;
        int err = 1 - x;

        while (x >= y)
        {
            Plot(cx + x, cy + y, px);
            Plot(cx + y, cy + x, px);
            Plot(cx - y, cy + x, px);
            Plot(cx - x, cy + y, px);
            Plot(cx - x, cy - y, px);
            Plot(cx - y, cy - x, px);
            Plot(cx + y, cy - x, px);
            Plot(cx + x, cy - y, px);
            y++;
            if (err < 0) err += 2 * y + 1;
            else { x--; err += 2 * (y - x + 1); }
        }
        _dirty = true;
    }

    public void FillCircle(int cx, int cy, int radius, Color4 color)
    {
        var px = Pixel.FromColor4(color);
        int r2 = radius * radius;
        int minY = Math.Max(0, cy - radius);
        int maxY = Math.Min(Height - 1, cy + radius);
        int minX = Math.Max(0, cx - radius);
        int maxX = Math.Min(Width - 1, cx + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - cy;
            int dy2 = dy * dy;
            int row = y * Width;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                if (dx * dx + dy2 <= r2)
                    _pixels[row + x] = px;
            }
        }
        _dirty = true;
    }

    public void Resize(int width, int height, bool preserve = false)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var next = new Pixel[width * height];
        if (preserve && _pixels.Length > 0)
        {
            int minW = Math.Min(width, Width);
            int minH = Math.Min(height, Height);
            for (int y = 0; y < minH; y++)
                Array.Copy(_pixels, y * Width, next, y * width, minW);
        }

        _pixels = next;
        Width = width;
        Height = height;

        _texture?.Dispose();
        _texture = null;
        _dirty = true;
    }

    /// <summary>
    /// Uploads dirty pixels to the GPU texture (allocating the OpenGL texture lazily).
    /// </summary>
    public void Upload()
    {
        if (_texture is null)
        {
            _texture = new Texture();
            _texture.Allocate(Width, Height, SizedInternalFormat.Rgba8, 1);
            _texture.ApplyOptions(_nearestFilter ? Texture.Options.Nearest : Texture.Options.Linear);
            _dirty = true;
        }

        if (_dirty)
        {
            _texture.Update<Pixel>(_pixels, 0, 0, Width, Height, PixelFormat.Rgba, PixelType.UnsignedByte);
            _dirty = false;
        }
    }

    private void Plot(int x, int y, Pixel px)
    {
        if (InBounds(x, y))
            _pixels[y * Width + x] = px;
    }

    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
        GC.SuppressFinalize(this);
    }
}
