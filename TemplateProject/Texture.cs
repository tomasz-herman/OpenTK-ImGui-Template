using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace TemplateProject;

public class Texture : IDisposable, IBindable
{
    public const string ResourcesPath = "TemplateProject.Resources";
    public int Handle { get; }

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

    public void LoadDataFromResources(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"{ResourcesPath}.{path}");

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
        if (levels < 0) levels = BitOperations.Log2((uint)Math.Max(width, height));
        GL.TextureStorage2D(Handle, levels, internalFormat, width, height);
    }
    
    public void Update(Array data, int x, int y, int width, int height, PixelFormat format, PixelType pixelType, int level = 0)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        Update(gcHandle.AddrOfPinnedObject(), x, y, width, height, format, pixelType, level);
        gcHandle.Free();
    }
    
    public void Update(IntPtr data, int x, int y, int width, int height, PixelFormat format, PixelType pixelType, int level = 0)
    {
        GL.TextureSubImage2D(Handle, level, x, y, width, height, format, pixelType, data);
    }

    public void ReadData(Array data, int bufferSize, PixelFormat format, PixelType type, int level = 0)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        ReadData(gcHandle.AddrOfPinnedObject(), bufferSize, format, type, level);
        gcHandle.Free();    
    }

    public void ReadData(IntPtr data, int bufferSize, PixelFormat format, PixelType type, int level = 0)
    {
        GL.GetTextureImage(Handle, level, format, type, bufferSize, data);
    }

    public void ApplyOptions(Options options)
    {
        foreach (var parameter in options.Parameters)
        {
            GL.TextureParameter(Handle, parameter.Key, Convert.ToInt32(parameter.Value));
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
            (TextureParameterName.TextureMinFilter, TextureMinFilter.LinearMipmapLinear),
            (TextureParameterName.TextureMagFilter, TextureMagFilter.Linear),
            (TextureParameterName.TextureWrapS, TextureWrapMode.Repeat),
            (TextureParameterName.TextureWrapT, TextureWrapMode.Repeat));

        public Dictionary<TextureParameterName, Enum> Parameters { get; } = new();

        public Options(params (TextureParameterName name, Enum value)[] parameters)
        {
            foreach (var (name, value) in parameters) SetParameter(name, value);
        }

        public Options SetParameter(TextureParameterName name, Enum value)
        {
            Parameters[name] = value;
            return this;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var (name, value) in Parameters)
            {
                builder.Append($"{name} : {value}\n");
            }
            return builder.ToString();
        }
    }
}