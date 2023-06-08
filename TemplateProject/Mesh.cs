using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace TemplateProject;

public class Mesh : IDisposable
{
    public int Handle { get; }
    private List<VertexBuffer> VertexBuffers { get; } = new();
    private IndexBuffer? IndexBuffer { get; }
    private PrimitiveType Type { get; }

    public Mesh(PrimitiveType type, IndexBuffer? indexBuffer, VertexBuffer vertexBuffer, params VertexBuffer[] vertexBuffers)
    {
        Type = type;
        Handle = GL.GenVertexArray();
        IndexBuffer = indexBuffer;
        VertexBuffers.Add(vertexBuffer);
        VertexBuffers.AddRange(vertexBuffers);
        GL.BindVertexArray(Handle);
        IndexBuffer?.Load();
        VertexBuffers.ForEach(buffer => buffer.Load());
        GL.BindVertexArray(0);
    }

    public void Render()
    {
        GL.BindVertexArray(Handle);
        if(IndexBuffer != null) GL.DrawElements(Type, IndexBuffer.Count, IndexBuffer.Type, IntPtr.Zero);
        else GL.DrawArrays(Type, 0, VertexBuffers[0].Count);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(Handle);
        VertexBuffers.ForEach(buffer => buffer.Dispose());
        IndexBuffer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class IndexBuffer : IDisposable
{
    public int Handle { get; private set; }
    public Array Data { get; }
    public IntPtr DataPtr { get; }
    public int Sizeof { get; }
    public int Count { get; set; }
    public DrawElementsType Type { get; }

    public IndexBuffer(Array data, int @sizeof, DrawElementsType type)
    {
        Data = data;
        GCHandle handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
        DataPtr = handle.AddrOfPinnedObject();
        handle.Free();
        Sizeof = @sizeof;
        Count = Data.Length;
        Type = type;
    }
    
    public void Load() {
        Handle = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Handle);
        GL.BufferData(BufferTarget.ElementArrayBuffer, Data.Length * Sizeof, DataPtr, BufferUsageHint.StaticDraw);
    }
    
    public void Update()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, Handle);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, Data.Length * Sizeof, DataPtr);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }
}

public class VertexBuffer : IDisposable
{
    public List<Attribute> Attributes { get; }
    public int Handle { get; private set; }
    public Array Data { get; }
    public IntPtr DataPtr { get; }
    public int Sizeof { get; }
    public int Count { get; set; }

    public VertexBuffer(Array data, int @sizeof, CreateLayout layout, params Attribute[] attributes)
    {
        Data = data;
        var handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
        DataPtr = handle.AddrOfPinnedObject();
        handle.Free();
        Sizeof = @sizeof;
        Count = Data.Length / attributes.Select(attrib => attrib.Size).Sum();
        layout(Count, Sizeof, attributes);
        Attributes = new List<Attribute>(attributes);
    }

    public void Load() {
        Handle = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, Handle);
        GL.BufferData(BufferTarget.ArrayBuffer, Data.Length * Sizeof, DataPtr, BufferUsageHint.StaticDraw);
        foreach (var attrib in Attributes)
        {
            GL.EnableVertexAttribArray(attrib.Index);
            GL.VertexAttribPointer(attrib.Index, attrib.Size, attrib.Type, attrib.Normalized, attrib.Stride, attrib.Offset);

        }
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void Update()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, Handle);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, Data.Length * Sizeof, DataPtr);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }

    public static void DontCreateLayout(int count, int @sizeof, params Attribute[] attributes) { }
    
    public static void CreateSimpleLayout(int count, int @sizeof, params Attribute[] attributes)
    {
        for (int index = 0, offset = 0; index < attributes.Length; index++)
        {
            attributes[index].Offset = offset;
            attributes[index].Stride = 0;
            offset += attributes[index].Size * count * @sizeof;
        }
    }
    
    public static void CreateInterleavedLayout(int count, int @sizeof, params Attribute[] attributes)
    {
        int stride = attributes.Select(attrib => attrib.Size).Sum() * @sizeof;
        for (int index = 0, offset = 0; index < attributes.Length; index++)
        {
            attributes[index].Offset = offset;
            attributes[index].Stride = stride;
            offset += attributes[index].Size * @sizeof;
        }
    }
}

public delegate void CreateLayout(int count, int @sizeof, params Attribute[] attributes);

public struct Attribute
{
    public int Index { get; set; }
    public int Size { get; set; }
    public VertexAttribPointerType Type { get; set; }
    public bool Normalized { get; set; }
    public int Stride { get; set; }
    public int Offset { get; set; }

    public Attribute(int index, int size, VertexAttribPointerType type = VertexAttribPointerType.Float, bool normalized = false, int stride = 0, int offset = 0)
    {
        Index = index;
        Size = size;
        Type = type;
        Normalized = normalized;
        Stride = stride;
        Offset = offset;
    }
}
