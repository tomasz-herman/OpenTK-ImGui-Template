using System.Reflection;
using System.Text;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace TemplateProject;

public class Texture : IDisposable
{
    private int _handle;

    public Texture(string path, Options options = default)
    {
        _handle = GL.GenTexture();
        Use();
        LoadTexture(path, options);
            
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void LoadTexture(string path, Options? options = null)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"TemplateProject.Resources.{path}");

        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        GL.TexImage2D(TextureTarget.Texture2D, 
            0, 
            PixelInternalFormat.Rgba, 
            image.Width, image.Height, 
            0, 
            PixelFormat.Rgba, 
            PixelType.UnsignedByte, 
            image.Data);

        foreach (var parameter in (options ?? new Options()).GetOptions())
        {
            GL.TexParameter(TextureTarget.Texture2D, parameter.Key, Convert.ToInt32(parameter.Value));
        }

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public void Use(TextureUnit unit = TextureUnit.Texture0)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(TextureTarget.Texture2D, _handle);
    }

    public void Dispose()
    {
        GL.DeleteTexture(_handle);
        GC.SuppressFinalize(this);
    }

    public class Options
    {
        private Dictionary<TextureParameterName, Enum> Parameters { get; } = new()
        {
            { TextureParameterName.TextureMinFilter, TextureMinFilter.LinearMipmapLinear },
            { TextureParameterName.TextureMagFilter, TextureMinFilter.Linear },
            { TextureParameterName.TextureWrapS, TextureWrapMode.Repeat },
            { TextureParameterName.TextureWrapT, TextureWrapMode.Repeat },
        };

        public Options(params (TextureParameterName name, Enum value)[] parameters)
        {
            foreach (var (name, value) in parameters) AddOption(name, value);
        }

        public void AddOption(TextureParameterName name, Enum value)
        {
            Parameters[name] = value;
        }

        public IEnumerable<(TextureParameterName Key, Enum Value)> GetOptions()
        {
            return Parameters.Select(x => (x.Key,x.Value));
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