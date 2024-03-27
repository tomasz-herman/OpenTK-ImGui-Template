using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace TemplateProject;

public class Mesh : IDisposable, IBindable
{
    public int Handle { get; }
    private List<VertexBuffer> VertexBuffers { get; } = new();
    private IndexBuffer? IndexBuffer { get; }
    private PrimitiveType Type { get; }

    public Mesh(PrimitiveType type, IndexBuffer? indexBuffer, VertexBuffer vertexBuffer, params VertexBuffer[] vertexBuffers)
    {
        Type = type;
        GL.CreateVertexArrays(1, out int handle);
        Handle = handle;
        IndexBuffer = indexBuffer;
        VertexBuffers.Add(vertexBuffer);
        VertexBuffers.AddRange(vertexBuffers);
        if (IndexBuffer != null)
        {
            GL.VertexArrayElementBuffer(Handle, IndexBuffer.Handle);        
        }

        for (var index = 0; index < VertexBuffers.Count; index++)
        {
            var buffer = VertexBuffers[index];
            buffer.CreateLayout(handle, index);
        }
    }

    public void Bind()
    {
        GL.BindVertexArray(Handle);
    }

    public void Unbind()
    {
        GL.BindVertexArray(0);
    }

    public void Render()
    {
        if (IndexBuffer != null) GL.DrawElements(Type, IndexBuffer.Count, IndexBuffer.Type, IntPtr.Zero);
        else GL.DrawArrays(Type, 0, VertexBuffers[0].Count);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(Handle);
        VertexBuffers.ForEach(buffer => buffer.Dispose());
        IndexBuffer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public interface IBuffer : IBindable
{
    public void Allocate(int size);
    public void Load(Array data, int size);
    public void Update(Array data, int dataOffset, int offset, int size);
    public IntPtr Map(BufferAccess access);
    public void Unmap();
}

public class IndexBuffer : IDisposable, IBuffer
{
    public BufferUsageHint Usage { get; }
    public int Handle { get; private set; }
    public DrawElementsType Type { get; }
    public int Count { get; set; }

    public IndexBuffer(DrawElementsType type, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw)
    {
        GL.CreateBuffers(1, out int handle);
        Handle = handle;
        Type = type;
        Usage = usage;
        Count = count;
    }

    public IndexBuffer(int size, DrawElementsType type, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw) : this(type, count, usage)
    {
        Allocate(size);
    }

    public IndexBuffer(Array data, int size, DrawElementsType type, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw) : this(type, count, usage)
    {
        Load(data, size);
    }

    public void Allocate(int size)
    {
        GL.NamedBufferData(Handle, size, IntPtr.Zero, Usage);
    }

    public void Load(Array data, int size)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        GL.NamedBufferData(Handle, size, gcHandle.AddrOfPinnedObject(), Usage);
        gcHandle.Free();
    }

    public void Update(Array data, int dataOffset, int offset, int size)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        GL.NamedBufferSubData(Handle, offset, size, gcHandle.AddrOfPinnedObject() + dataOffset);
        gcHandle.Free();
    }

    public IntPtr Map(BufferAccess access)
    {
        return GL.MapNamedBuffer(Handle, BufferAccess.ReadWrite);
    }

    public void Unmap()
    {
        GL.UnmapNamedBuffer(Handle);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }

    public void Bind()
    {
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Handle);
    }

    public void Unbind()
    {
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }
}

public class VertexBuffer : IBuffer, IDisposable
{
    public Attribute[] Attributes { get; }
    public BufferUsageHint Usage { get; }
    public int Handle { get; }
    public int Count { get; set; }

    public VertexBuffer(int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw, params Attribute[] attributes)
    {
        GL.CreateBuffers(1, out int handle);
        Handle = handle;
        Usage = usage;
        Count = count;
        Attributes = attributes;
    }

    public VertexBuffer(int size, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw, params Attribute[] attributes) : this(count, usage, attributes)
    {
        Allocate(size);
    }

    public VertexBuffer(Array data, int size, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw, params Attribute[] attributes) : this(count, usage, attributes)
    {
        Load(data, size);
    }

    public void Allocate(int size)
    {
        GL.NamedBufferData(Handle, size, IntPtr.Zero, Usage);
    }

    public void CreateLayout(int vao, int index)
    {
        int stride = Attributes.Select(attrib => attrib.Size).Sum();
        for (int i = 0, offset = 0; i < Attributes.Length; i++)
        {
            Attributes[i].Offset = offset;
            Attributes[i].Stride = stride;
            offset += Attributes[index].Size;
        }
        GL.VertexArrayVertexBuffer(vao, index, Handle, IntPtr.Zero, stride);
        foreach (var attrib in Attributes)
        {
            GL.EnableVertexArrayAttrib(vao, attrib.Index);
            GL.VertexArrayAttribBinding(vao, attrib.Index, index);
            GL.VertexArrayAttribFormat(vao, attrib.Index, attrib.Count, attrib.Type, attrib.Normalized, attrib.Offset);
        }
    }

    public void Load(Array data, int size)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        GL.NamedBufferData(Handle, size, gcHandle.AddrOfPinnedObject(), Usage);
        gcHandle.Free();
    }

    public void Update(Array data, int dataOffset, int offset, int size)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        GL.NamedBufferSubData(Handle, offset, size, gcHandle.AddrOfPinnedObject() + dataOffset);
        gcHandle.Free();
    }

    public IntPtr Map(BufferAccess access)
    {
        return GL.MapNamedBuffer(Handle, access);
    }

    public void Unmap()
    {
        GL.UnmapNamedBuffer(Handle);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }

    public void Bind()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, Handle);
    }

    public void Unbind()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }
}

public class Attribute
{
    public int Index { get; set; }
    public int Count { get; set; }
    public VertexAttribType Type { get; set; }
    public bool Normalized { get; set; }
    public int Stride { get; set; }
    public int Offset { get; set; }
    public int Size => Count * Sizes[Type];

    private static Dictionary<VertexAttribType, int> Sizes { get; } =
        new()
        {
            { VertexAttribType.Byte, 1 },
            { VertexAttribType.UnsignedByte, 1 },
            { VertexAttribType.Short, 2 },
            { VertexAttribType.UnsignedShort, 2 },
            { VertexAttribType.HalfFloat, 2 },
            { VertexAttribType.Int, 4 },
            { VertexAttribType.UnsignedInt, 4 },
            { VertexAttribType.Float, 4 },
            { VertexAttribType.Fixed, 4 },
            { VertexAttribType.Int2101010Rev, 4 },
            { VertexAttribType.UnsignedInt2101010Rev, 4 },
            { VertexAttribType.UnsignedInt10F11F11FRev, 4 },
            { VertexAttribType.Double, 8 }
        };
    public Attribute(int index, int count, VertexAttribType type = VertexAttribType.Float, bool normalized = false, int stride = 0, int offset = 0)
    {
        Index = index;
        Count = count;
        Type = type;
        Normalized = normalized;
        Stride = stride;
        Offset = offset;
    }
}
