using System.Diagnostics;
using OpenTK.Graphics.OpenGL;

namespace TemplateProject;

public static class OpenGLUtils
{
    [Conditional("DEBUG")]
    public static void CheckError()
    {
        ErrorCode error;
        while ((error = GL.GetError()) != ErrorCode.NoError)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            Debug.Print($"Error: {error.ToString()}({(int)error})");
        }
    }
}