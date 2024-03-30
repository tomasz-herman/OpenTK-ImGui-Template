using System.Reflection;
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
        Handle = GL.GenTexture();
    }

    public Texture(string path, bool resource = true, Options? options = null, bool generateMipmaps = true)
    {
        Handle = GL.GenTexture();

        Bind();

        if (resource) LoadDataFromResources(path);
        else LoadDataFromFile(path);

        ApplyOptions(options ?? Options.Default);

        if (generateMipmaps) GenerateMipmaps();

        Unbind();
    }

    public void LoadDataFromResources(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"{ResourcesPath}.{path}");

        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        LoadData(image.Data, image.Width, image.Height,
            PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
    }

    public void LoadDataFromFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);

        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        LoadData(image.Data, image.Width, image.Height,
            PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
    }

    public void LoadData<T>(T[] data, int width, int height,
        PixelInternalFormat internalFormat, PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.TexImage2D(TextureTarget.Texture2D, level, internalFormat,
            width, height, 0, format, type, data);
    }

    public void LoadData<T>(T[,] data, int width, int height,
        PixelInternalFormat internalFormat, PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.TexImage2D(TextureTarget.Texture2D, level, internalFormat,
            width, height, 0, format, type, data);
    }

    public void LoadData<T>(T[,,] data, int width, int height,
        PixelInternalFormat internalFormat, PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.TexImage2D(TextureTarget.Texture2D, level, internalFormat,
            width, height, 0, format, type, data);
    }

    public void LoadData(IntPtr data, int width, int height,
        PixelInternalFormat internalFormat, PixelFormat format, PixelType type, int level = 0)
    {
        GL.TexImage2D(TextureTarget.Texture2D, level, internalFormat,
            width, height, 0, format, type, data);
    }

    public void UpdateData<T>(T[] data, int xOffset, int yOffset, int width, int height,
        PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.TexSubImage2D(TextureTarget.Texture2D, level, xOffset, yOffset, width, height, format, type, data);
    }

    public void UpdateData<T>(T[,] data, int xOffset, int yOffset, int width, int height,
        PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.TexSubImage2D(TextureTarget.Texture2D, level, xOffset, yOffset, width, height, format, type, data);
    }

    public void UpdateData<T>(T[,,] data, int xOffset, int yOffset, int width, int height,
        PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.TexSubImage2D(TextureTarget.Texture2D, level, xOffset, yOffset, width, height, format, type, data);
    }

    public void UpdateData(IntPtr data, int xOffset, int yOffset, int width, int height,
        PixelFormat format, PixelType type, int level = 0)
    {
        GL.TexSubImage2D(TextureTarget.Texture2D, level, xOffset, yOffset, width, height, format, type, data);
    }

    public void ReadData<T>(T[] data, PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.GetTexImage(TextureTarget.Texture2D, level, format, type, data);
    }

    public void ReadData<T>(T[,] data, PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.GetTexImage(TextureTarget.Texture2D, level, format, type, data);
    }

    public void ReadData<T>(T[,,] data, PixelFormat format, PixelType type, int level = 0)
        where T : struct
    {
        GL.GetTexImage(TextureTarget.Texture2D, level, format, type, data);
    }

    public void ReadData(IntPtr data, PixelFormat format, PixelType type, int level = 0)
    {
        GL.GetTexImage(TextureTarget.Texture2D, level, format, type, data);
    }

    public void ApplyOptions(Options options)
    {
        foreach (var parameter in options.Parameters)
        {
            GL.TexParameter(TextureTarget.Texture2D, parameter.Key, Convert.ToInt32(parameter.Value));
        }
    }

    public void GenerateMipmaps()
    {
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public void ActivateUnit(TextureUnit unit = TextureUnit.Texture0)
    {
        GL.ActiveTexture(unit);
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