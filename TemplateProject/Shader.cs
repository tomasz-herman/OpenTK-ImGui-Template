using System.Reflection;
using System.Text;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TemplateProject;

public class Shader : IDisposable
{
    public const string ResourcesPath = "TemplateProject.Resources";
    public int Handle { get; private set; }
    private Dictionary<string, int> Uniforms { get; } = new();

    public Shader(params (string path, ShaderType type)[] paths)
    {
        var sources = new List<(string source, ShaderType type)>();
        foreach (var (path, type) in paths)
        {
            sources.Add((ReadSource(path), type));
        }

        var shaders = new List<int>();
        foreach (var (source, type) in sources)
        {
            shaders.Add(CreateShader(source, type));
        }

        foreach (var shader in shaders)
        {
            CompileShader(shader);
        }

        CreateProgram(shaders.ToArray());

        CleanupShaders(shaders.ToArray());

        InitializeUniformsMap();
    }

    private string ReadSource(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"{ResourcesPath}.{path}");
        if (stream == null) throw new Exception("Shader not found!");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private int CreateShader(string source, ShaderType type)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        return shader;
    }

    private void CompileShader(int shader)
    {
        GL.CompileShader(shader);

        var log = GL.GetShaderInfoLog(shader);
        if (log != string.Empty) Console.WriteLine(log);
    }

    private void CreateProgram(params int[] shaders)
    {
        Handle = GL.CreateProgram();

        foreach (var shader in shaders)
        {
            GL.AttachShader(Handle, shader);
        }

        GL.LinkProgram(Handle);
    }

    private void CleanupShaders(params int[] shaders)
    {
        foreach (var shader in shaders)
        {
            GL.DetachShader(Handle, shader);
            GL.DeleteShader(shader);
        }
    }

    private void InitializeUniformsMap()
    {
        GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms, out int uniforms);
        for (int i = 0; i < uniforms; i++)
        {
            GL.GetActiveUniform(Handle, i, 64,
                out _, out _, out _, out string name);
            Uniforms[name] = GL.GetUniformLocation(Handle, name);
        }
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }

    private int GetUniformLocation(string name)
    {
        bool found = Uniforms.TryGetValue(name, out int location);
        if (found) return location;
        Console.WriteLine($"Uniform with name {name} not found.");
        return -1;
    }

    public void LoadInteger(string name, int value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
    }

    public void LoadFloat(string name, float value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
    }

    public void LoadFloat3(string name, Vector3 value)
    {
        GL.Uniform3(GetUniformLocation(name), ref value);
    }

    public void LoadFloat3(string name, ref Vector3 value)
    {
        GL.Uniform3(GetUniformLocation(name), ref value);
    }

    public void LoadFloat4(string name, Vector4 value)
    {
        GL.Uniform4(GetUniformLocation(name), ref value);
    }

    public void LoadFloat4(string name, ref Vector4 value)
    {
        GL.Uniform4(GetUniformLocation(name), ref value);
    }

    public void LoadMatrix4(string name, Matrix4 value)
    {
        GL.UniformMatrix4(GetUniformLocation(name), false, ref value);
    }

    public void LoadMatrix4(string name, ref Matrix4 value)
    {
        GL.UniformMatrix4(GetUniformLocation(name), false, ref value);
    }

    public void Dispatch(int x, int y, int z)
    {
        GL.DispatchCompute(x, y, z);
    }

    public void Wait(MemoryBarrierFlags flags = MemoryBarrierFlags.AllBarrierBits)
    {
        GL.MemoryBarrier(flags);
    }

    public void Dispose()
    {
        GL.DeleteProgram(Handle);
        GC.SuppressFinalize(this);
    }
}