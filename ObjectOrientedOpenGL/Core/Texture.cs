using System.Numerics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace ObjectOrientedOpenGL.Core;

public class Texture : IDisposable, IBindable
{
    public int Handle { get; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Texture()
    {
        GL.CreateTextures(TextureTarget.Texture2D, 1, out int handle);
        Handle = handle;
    }

    public Texture(string path, bool resource = true, Options? options = null, bool generateMipmaps = true) : this()
    {
        if (resource) LoadDataFromResources(path);
        else LoadDataFromFile(path);

        ApplyOptions(options ?? Options.Default);

        if (generateMipmaps) GenerateMipmaps();
    }

    public static Texture FromMemory(ReadOnlySpan<byte> encodedImageBytes, Options? options = null, bool generateMipmaps = true)
    {
        using var ms = new MemoryStream(encodedImageBytes.ToArray());
        ImageResult image = ImageResult.FromStream(ms, ColorComponents.RedGreenBlueAlpha);
        return FromRgba(image.Data, image.Width, image.Height, options, generateMipmaps);
    }

    public static Texture FromRgba(ReadOnlySpan<byte> rgbaPixels, int width, int height, Options? options = null, bool generateMipmaps = true)
    {
        var tex = new Texture();
        tex.Allocate(width, height, SizedInternalFormat.Rgba8);
        tex.Update(rgbaPixels, 0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte);
        tex.ApplyOptions(options ?? Options.Default);
        if (generateMipmaps) tex.GenerateMipmaps();
        return tex;
    }

    public static Texture CreateSolidColor(byte r, byte g, byte b, byte a = 255)
    {
        var tex = new Texture();
        tex.Allocate(1, 1, SizedInternalFormat.Rgba8, 1);
        byte[] px = [r, g, b, a];
        tex.Update(px, 0, 0, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte);
        tex.ApplyOptions(Options.Nearest);
        return tex;
    }

    public void LoadDataFromResources(string path)
    {
        using var stream = ResourcesUtils.GetResourceStream(path)
            ?? throw new FileNotFoundException($"Embedded texture resource not found: {path}");

        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        Allocate(image.Width, image.Height, SizedInternalFormat.Rgba8);
        Update(image.Data, 0, 0, image.Width, image.Height, PixelFormat.Rgba, PixelType.UnsignedByte);
    }

    public void LoadDataFromFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);

        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        Allocate(image.Width, image.Height, SizedInternalFormat.Rgba8);
        Update(image.Data, 0, 0, image.Width, image.Height, PixelFormat.Rgba, PixelType.UnsignedByte);
    }

    public void Allocate(int width, int height, SizedInternalFormat internalFormat, int levels = -1)
    {
        Width = width;
        Height = height;
        if (levels < 0) levels = Math.Max(1, BitOperations.Log2((uint)Math.Max(1, Math.Max(width, height))) + 1);
        GL.TextureStorage2D(Handle, levels, internalFormat, width, height);
    }

    public unsafe void Update<T>(ReadOnlySpan<T> data, int x, int y, int width, int height, PixelFormat format, PixelType pixelType,
        int level = 0) where T : unmanaged
    {
        fixed (T* ptr = data)
        {
            Update((nint)ptr, x, y, width, height, format, pixelType, level);
        }
    }

    public void Update(Array data, int x, int y, int width, int height, PixelFormat format, PixelType pixelType,
        int level = 0)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            Update(gcHandle.AddrOfPinnedObject(), x, y, width, height, format, pixelType, level);
        }
        finally
        {
            gcHandle.Free();
        }
    }

    public void Update(IntPtr data, int x, int y, int width, int height, PixelFormat format, PixelType pixelType,
        int level = 0)
    {
        GL.TextureSubImage2D(Handle, level, x, y, width, height, format, pixelType, data);
    }

    public void ReadData(Array data, int bufferSize, PixelFormat format, PixelType type, int level = 0)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            ReadData(gcHandle.AddrOfPinnedObject(), bufferSize, format, type, level);
        }
        finally
        {
            gcHandle.Free();
        }
    }

    public void ReadData(IntPtr data, int bufferSize, PixelFormat format, PixelType type, int level = 0)
    {
        GL.GetTextureImage(Handle, level, format, type, bufferSize, data);
    }

    public void ApplyOptions(Options options)
    {
        foreach (var parameter in options.Parameters.Values)
        {
            parameter.Apply(Handle);
        }
    }

    public void GenerateMipmaps()
    {
        GL.GenerateTextureMipmap(Handle);
    }

    public void ActivateUnit(int unit = 0)
    {
        GL.BindTextureUnit(unit, Handle);
    }

    public void Bind()
    {
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Unbind()
    {
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Dispose()
    {
        GL.DeleteTexture(Handle);
        GC.SuppressFinalize(this);
    }

    public class Options
    {
        public static Options Default => new(
            new EnumParameter(TextureParameterName.TextureMinFilter, TextureMinFilter.LinearMipmapLinear),
            new EnumParameter(TextureParameterName.TextureMagFilter, TextureMagFilter.Linear),
            new EnumParameter(TextureParameterName.TextureWrapS, TextureWrapMode.Repeat),
            new EnumParameter(TextureParameterName.TextureWrapT, TextureWrapMode.Repeat));

        public static Options Nearest => new(
            new EnumParameter(TextureParameterName.TextureMinFilter, TextureMinFilter.Nearest),
            new EnumParameter(TextureParameterName.TextureMagFilter, TextureMagFilter.Nearest),
            new EnumParameter(TextureParameterName.TextureWrapS, TextureWrapMode.ClampToEdge),
            new EnumParameter(TextureParameterName.TextureWrapT, TextureWrapMode.ClampToEdge));

        public static Options Linear => new(
            new EnumParameter(TextureParameterName.TextureMinFilter, TextureMinFilter.Linear),
            new EnumParameter(TextureParameterName.TextureMagFilter, TextureMagFilter.Linear),
            new EnumParameter(TextureParameterName.TextureWrapS, TextureWrapMode.ClampToEdge),
            new EnumParameter(TextureParameterName.TextureWrapT, TextureWrapMode.ClampToEdge));

        public Dictionary<TextureParameterName, IParameter> Parameters { get; } = new();

        public Options(params IParameter[] parameters)
        {
            foreach (var param in parameters) SetParameter(param);
        }

        public Options SetParameter(IParameter param)
        {
            Parameters[param.Name] = param;
            return this;
        }

        public Options SetEnum(TextureParameterName name, Enum value)
        {
            Parameters[name] = new EnumParameter(name, value);
            return this;
        }

        public Options SetInt(TextureParameterName name, params int[] value)
        {
            Parameters[name] = new IntParameter(name, value);
            return this;
        }

        public Options SetFloat(TextureParameterName name, params float[] value)
        {
            Parameters[name] = new FloatParameter(name, value);
            return this;
        }
    }

    public interface IParameter
    {
        TextureParameterName Name { get; }
        void Apply(int texture);
    }

    public class IntParameter(TextureParameterName name, params int[] value) : IParameter
    {
        public TextureParameterName Name { get; } = name;
        public int[] Value { get; } = value;

        public void Apply(int texture)
        {
            if (Value.Length == 1)
                GL.TextureParameter(texture, Name, Value[0]);
            else
                GL.TextureParameter(texture, Name, Value);
        }
    }

    public class EnumParameter(TextureParameterName name, Enum value) : IntParameter(name, Convert.ToInt32(value));

    public class FloatParameter(TextureParameterName name, params float[] value) : IParameter
    {
        public TextureParameterName Name { get; } = name;
        public float[] Value { get; } = value;

        public void Apply(int texture)
        {
            GL.TextureParameter(texture, Name, Value);
        }
    }
}
