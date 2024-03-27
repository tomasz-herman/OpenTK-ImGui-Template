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
        Bind();
        IndexBuffer?.Bind();
        foreach (var buffer in VertexBuffers)
        {
            buffer.Bind();
            buffer.CreateLayout();
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
    public CreateLayout Layout { get; }
    public BufferUsageHint Usage { get; }
    public int Handle { get; }
    public int Count { get; set; }

    public VertexBuffer(CreateLayout layout, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw, params Attribute[] attributes)
    {
        GL.CreateBuffers(1, out int handle);
        Handle = handle;
        Layout = layout;
        Usage = usage;
        Count = count;
        Attributes = attributes;
    }

    public VertexBuffer(int size, CreateLayout layout, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw, params Attribute[] attributes) : this(layout, count, usage, attributes)
    {
        Allocate(size);
    }

    public VertexBuffer(Array data, int size, CreateLayout layout, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw, params Attribute[] attributes) : this(layout, count, usage, attributes)
    {
        Load(data, size);
    }

    public void Allocate(int size)
    {
        GL.NamedBufferData(Handle, size, IntPtr.Zero, Usage);
    }

    public void CreateLayout()
    {
        Layout(Count, Attributes);
        foreach (var attrib in Attributes)
        {
            GL.EnableVertexAttribArray(attrib.Index);
            attrib.Setup();
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

    public static void DontCreateLayout(int count, params Attribute[] attributes) { }

    public static void CreateSimpleLayout(int count, params Attribute[] attributes)
    {
        for (int index = 0, offset = 0; index < attributes.Length; index++)
        {
            attributes[index].Offset = offset;
            attributes[index].Stride = 0;
            offset += attributes[index].Size * count;
        }
    }

    public static void CreateInterleavedLayout(int count, params Attribute[] attributes)
    {
        int stride = attributes.Select(attrib => attrib.Size).Sum();
        for (int index = 0, offset = 0; index < attributes.Length; index++)
        {
            attributes[index].Offset = offset;
            attributes[index].Stride = stride;
            offset += attributes[index].Size;
        }
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

public delegate void CreateLayout(int count, params Attribute[] attributes);

public class Attribute
{
    public int Index { get; set; }
    public int Count { get; set; }
    public VertexAttribPointerType Type { get; set; }
    public bool Normalized { get; set; }
    public int Stride { get; set; }
    public int Offset { get; set; }
    public int Size => Count * Sizes[Type];
    public Action Setup => () => Setups[Type](this);

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

    private static readonly Action<Attribute> FloatSetup = attr =>
        GL.VertexAttribPointer(attr.Index, attr.Count, attr.Type, attr.Normalized, attr.Stride, attr.Offset);

    private static readonly Action<Attribute> IntSetup = attr =>
        GL.VertexAttribIPointer(attr.Index, attr.Size, (VertexAttribIntegerType)attr.Type, attr.Stride, new IntPtr(attr.Offset));

    private static Dictionary<VertexAttribPointerType, Action<Attribute>> Setups { get; } =
        new()
        {
            { VertexAttribPointerType.Byte, IntSetup },
            { VertexAttribPointerType.UnsignedByte, IntSetup },
            { VertexAttribPointerType.Short, IntSetup },
            { VertexAttribPointerType.UnsignedShort, IntSetup },
            { VertexAttribPointerType.HalfFloat, FloatSetup },
            { VertexAttribPointerType.Int, IntSetup },
            { VertexAttribPointerType.UnsignedInt, IntSetup },
            { VertexAttribPointerType.Float, FloatSetup },
            { VertexAttribPointerType.Fixed, FloatSetup },
            { VertexAttribPointerType.Int2101010Rev, FloatSetup },
            { VertexAttribPointerType.UnsignedInt2101010Rev, FloatSetup },
            { VertexAttribPointerType.UnsignedInt10F11F11FRev, FloatSetup },
            { VertexAttribPointerType.Double, FloatSetup }
        };

    public Attribute(int index, int count, VertexAttribPointerType type = VertexAttribPointerType.Float, bool normalized = false, int stride = 0, int offset = 0)
    {
        Index = index;
        Count = count;
        Type = type;
        Normalized = normalized;
        Stride = stride;
        Offset = offset;
    }
}
