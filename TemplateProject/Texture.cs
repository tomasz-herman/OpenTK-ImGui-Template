using System.Reflection;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace TemplateProject
{
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

        private void LoadTexture(string path, Options options = default)
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
            
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int) options.MinFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int) options.MagFilter);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) options.WrapS);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) options.WrapT);
            
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

        public struct Options
        {
            public TextureMinFilter MinFilter = TextureMinFilter.LinearMipmapLinear;
            public TextureMagFilter MagFilter = TextureMagFilter.Linear;
            public TextureWrapMode WrapS = TextureWrapMode.Repeat;
            public TextureWrapMode WrapT = TextureWrapMode.Repeat;

            public Options() { }
        }
    }
}