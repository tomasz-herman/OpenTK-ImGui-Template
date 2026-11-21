using System.Text;
using System.Text.RegularExpressions;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Core;

public class Shader : IDisposable
{
    public int Handle { get; private set; }
    private Dictionary<string, int> Uniforms { get; } = new();

    public Shader(params (string path, ShaderType type)[] paths)
    {
        var sources = paths.Select(p => (ReadSource(p.path), p.type)).ToArray();
        Build(sources);
    }

    private Shader()
    {
    }

    public static Shader FromSources(params (string source, ShaderType type)[] sources)
    {
        var shader = new Shader();
        shader.Build(sources);
        return shader;
    }

    private void Build((string source, ShaderType type)[] sources)
    {
        int[] shaders = new int[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            shaders[i] = CreateShader(sources[i].source, sources[i].type);
            CompileShader(shaders[i], sources[i].type);
        }

        CreateProgram(shaders);
        CleanupShaders(shaders);
        InitializeUniformsMap();
    }

    private static string ReadSource(string path)
    {
        using var stream = ResourcesUtils.GetResourceStream(path);
        if (stream == null) throw new FileNotFoundException($"Shader resource not found: {path}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static int CreateShader(string source, ShaderType type)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        return shader;
    }

    private static void CompileShader(int shader, ShaderType type)
    {
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        var log = GL.GetShaderInfoLog(shader);
        if (status == 0)
            throw new InvalidOperationException($"{type} compilation failed:\n{log}");
        if (!string.IsNullOrWhiteSpace(log))
            Console.WriteLine(log);
    }

    private void CreateProgram(params int[] shaders)
    {
        Handle = GL.CreateProgram();

        foreach (var shader in shaders)
            GL.AttachShader(Handle, shader);

        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int status);
        var log = GL.GetProgramInfoLog(Handle);
        if (status == 0)
            throw new InvalidOperationException($"Shader program link failed:\n{log}");
        if (!string.IsNullOrWhiteSpace(log))
            Console.WriteLine(log);
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
        GL.GetProgram(Handle, GetProgramParameterName.ActiveUniformMaxLength, out int maxUniformLength);
        if (maxUniformLength <= 0) return;

        for (int i = 0; i < uniforms; i++)
        {
            GL.GetActiveUniform(Handle, i, maxUniformLength,
                out _, out int size, out _, out string nameTemplate);

            for (int j = 0; j < size; j++)
            {
                string name = Regex.Replace(nameTemplate, @"\[0\]$", $"[{j}]");
                Uniforms[name] = GL.GetUniformLocation(Handle, name);
            }
        }
    }

    public void Use() => GL.UseProgram(Handle);

    public bool HasUniform(string name) => Uniforms.TryGetValue(name, out int loc) && loc >= 0;

    private int GetUniformLocation(string name)
    {
        if (Uniforms.TryGetValue(name, out int location))
            return location;
        return -1;
    }

    public int GetUniformBlockIndex(string name) => GL.GetUniformBlockIndex(Handle, name);

    public void LoadInteger(string name, int value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) GL.ProgramUniform1(Handle, loc, value);
    }

    public void LoadFloat(string name, float value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) GL.ProgramUniform1(Handle, loc, value);
    }

    public void LoadFloat2(string name, Vector2 value) => LoadFloat2(name, ref value);

    public void LoadFloat2(string name, ref Vector2 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) GL.ProgramUniform2(Handle, loc, ref value);
    }

    public void LoadFloat3(string name, Vector3 value) => LoadFloat3(name, ref value);

    public void LoadFloat3(string name, ref Vector3 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) GL.ProgramUniform3(Handle, loc, ref value);
    }

    public void LoadFloat4(string name, Vector4 value) => LoadFloat4(name, ref value);

    public void LoadFloat4(string name, ref Vector4 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) GL.ProgramUniform4(Handle, loc, ref value);
    }

    public void LoadMatrix2(string name, Matrix2 value, bool transpose = false) => LoadMatrix2(name, ref value, transpose);

    public void LoadMatrix2(string name, ref Matrix2 value, bool transpose = false)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) GL.ProgramUniformMatrix2(Handle, loc, transpose, ref value);
    }

    public void LoadMatrix3(string name, Matrix3 value, bool transpose = false) => LoadMatrix3(name, ref value, transpose);

    public void LoadMatrix3(string name, ref Matrix3 value, bool transpose = false)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) GL.ProgramUniformMatrix3(Handle, loc, transpose, ref value);
    }

    public void LoadMatrix4(string name, Matrix4 value, bool transpose = false) => LoadMatrix4(name, ref value, transpose);

    public void LoadMatrix4(string name, ref Matrix4 value, bool transpose = false)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) GL.ProgramUniformMatrix4(Handle, loc, transpose, ref value);
    }

    public void Dispatch(int x, int y, int z) => GL.DispatchCompute(x, y, z);

    public void Wait(MemoryBarrierFlags flags = MemoryBarrierFlags.AllBarrierBits) => GL.MemoryBarrier(flags);

    public void Dispose()
    {
        GL.DeleteProgram(Handle);
        GC.SuppressFinalize(this);
    }
}
