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
        if (IndexBuffer != null) GL.DrawElements(Type, IndexBuffer.Count, IndexBuffer.Type, IntPtr.Zero);
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
    public BufferUsageHint Usage { get; }
    public int Handle { get; private set; }
    public Array Data { get; }
    public int Sizeof { get; }
    public int Count { get; }
    public DrawElementsType Type { get; }

    public IndexBuffer(Array data, int @sizeof, int count, DrawElementsType type, BufferUsageHint usage = BufferUsageHint.StaticDraw)
    {
        Data = data;
        Sizeof = @sizeof;
        Count = count;
        Type = type;
        Usage = usage;
    }

    public void Load()
    {
        var gcHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
        Handle = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Handle);
        GL.BufferData(BufferTarget.ElementArrayBuffer, Data.Length * Sizeof, gcHandle.AddrOfPinnedObject(), Usage);
        gcHandle.Free();
    }

    public void Update()
    {
        var gcHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Handle);
        GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, Data.Length * Sizeof, gcHandle.AddrOfPinnedObject());
        gcHandle.Free();
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
    public BufferUsageHint Usage { get; }
    public int Handle { get; }
    public Array Data { get; }
    public int Sizeof { get; }
    public int Count { get; }

    public VertexBuffer(Array data, int @sizeof, int count, CreateLayout layout, BufferUsageHint usage = BufferUsageHint.StaticDraw, params Attribute[] attributes)
    {
        Handle = GL.GenBuffer();
        Data = data;
        Sizeof = @sizeof;
        Count = count;
        Usage = usage;
        layout(count, attributes);
        Attributes = new List<Attribute>(attributes);
    }

    public void Load()
    {
        var gcHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
        GL.BindBuffer(BufferTarget.ArrayBuffer, Handle);
        GL.BufferData(BufferTarget.ArrayBuffer, Data.Length * Sizeof, gcHandle.AddrOfPinnedObject(), Usage);
        foreach (var attrib in Attributes)
        {
            GL.EnableVertexAttribArray(attrib.Index);
            GL.VertexAttribPointer(attrib.Index, attrib.Size, attrib.Type, attrib.Normalized, attrib.Stride, attrib.Offset);
        }
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        gcHandle.Free();
    }

    public void Update()
    {
        var gcHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
        GL.BindBuffer(BufferTarget.ArrayBuffer, Handle);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, Data.Length * Sizeof, gcHandle.AddrOfPinnedObject());
        gcHandle.Free();
    }

    public void Dispose()
    {
        GL.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }

    public static void DontCreateLayout(int count, params Attribute[] attributes) { }

    public static void CreateSimpleLayout(int count, params Attribute[] attributes)
    {
        for (int index = 0, offset = 0; index < attributes.Length; index++)
        {
            attributes[index].Offset = offset;
            attributes[index].Stride = 0;
            offset += attributes[index].Sizeof * count;
        }
    }

    public static void CreateInterleavedLayout(int count, params Attribute[] attributes)
    {
        int stride = attributes.Select(attrib => attrib.Sizeof).Sum();
        for (int index = 0, offset = 0; index < attributes.Length; index++)
        {
            attributes[index].Offset = offset;
            attributes[index].Stride = stride;
            offset += attributes[index].Sizeof;
        }
    }
}

public delegate void CreateLayout(int count, params Attribute[] attributes);

public struct Attribute
{
    public int Index { get; set; }
    public int Size { get; set; }
    public VertexAttribPointerType Type { get; set; }
    public bool Normalized { get; set; }
    public int Stride { get; set; }
    public int Offset { get; set; }
    public int Sizeof => Size * Sizes[Type];

    private static Dictionary<VertexAttribPointerType, int> Sizes { get; } =
        new()
        {
            { VertexAttribPointerType.Byte, 1 },
            { VertexAttribPointerType.UnsignedByte, 1 },
            { VertexAttribPointerType.Short, 2 },
            { VertexAttribPointerType.UnsignedShort, 2 },
            { VertexAttribPointerType.HalfFloat, 2 },
            { VertexAttribPointerType.Int, 4 },
            { VertexAttribPointerType.UnsignedInt, 4 },
            { VertexAttribPointerType.Float, 4 },
            { VertexAttribPointerType.Fixed, 4 },
            { VertexAttribPointerType.Int2101010Rev, 4 },
            { VertexAttribPointerType.UnsignedInt2101010Rev, 4 },
            { VertexAttribPointerType.UnsignedInt10F11F11FRev, 4 },
            { VertexAttribPointerType.Double, 8 }
        };

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
