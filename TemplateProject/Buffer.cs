using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace TemplateProject;

public abstract class Buffer : IBindable, IDisposable
{
    public abstract BufferTarget Target { get; }
    public BufferUsageHint Usage { get; protected set; }
    public int Handle { get; protected set; }

    public Buffer(BufferUsageHint usage)
    {
        GL.CreateBuffers(1, out int handle);
        Handle = handle;
        Usage = usage;
    }

    public void Allocate(int size)
    {
        GL.NamedBufferData(Handle, size, IntPtr.Zero, Usage);
    }

    public void Load(Array data, int size)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        Load(gcHandle.AddrOfPinnedObject(), size);
        gcHandle.Free();
    }

    public void Load(IntPtr data, int size)
    {
        GL.NamedBufferData(Handle, size, data, Usage);
    }

    public void Update(Array data, int dataOffset, int offset, int size)
    {
        var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        Update(gcHandle.AddrOfPinnedObject(), dataOffset, offset, size);
        gcHandle.Free();
    }

    public void Update(IntPtr data, int dataOffset, int offset, int size)
    {
        GL.NamedBufferSubData(Handle, offset, size, data + dataOffset);
    }

    public IntPtr Map(BufferAccess access = BufferAccess.ReadWrite)
    {
        return GL.MapNamedBuffer(Handle, access);
    }

    public void Unmap()
    {
        GL.UnmapNamedBuffer(Handle);
    }

    public void Bind()
    {
        GL.BindBuffer(Target, Handle);
    }

    public void Unbind()
    {
        GL.BindBuffer(Target, 0);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }
}

public class IndexBuffer : Buffer
{
    public override BufferTarget Target => BufferTarget.ElementArrayBuffer;
    public DrawElementsType ElementsType { get; }
    public int Count { get; set; }

    public IndexBuffer(DrawElementsType elementsType, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw) : base(usage)
    {
        ElementsType = elementsType;
        Count = count;
    }

    public IndexBuffer(int size, DrawElementsType elementsType, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw) : this(elementsType, count, usage)
    {
        Allocate(size);
    }

    public IndexBuffer(Array data, int size, DrawElementsType elementsType, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw) : this(elementsType, count, usage)
    {
        Load(data, size);
    }

    public IndexBuffer(IntPtr data, int size, DrawElementsType elementsType, int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw) : this(elementsType, count, usage)
    {
        Load(data, size);
    }
}

public class UniformBuffer : Buffer
{
    public override BufferTarget Target => BufferTarget.UniformBuffer;

    public UniformBuffer(BufferUsageHint usage = BufferUsageHint.DynamicCopy) : base(usage)
    {

    }

    public UniformBuffer(int size, BufferUsageHint usage = BufferUsageHint.DynamicCopy) : this(usage)
    {
        Allocate(size);
    }

    public UniformBuffer(Array data, int size, BufferUsageHint usage = BufferUsageHint.DynamicCopy) : this(usage)
    {
        Load(data, size);
    }

    public UniformBuffer(IntPtr data, int size, BufferUsageHint usage = BufferUsageHint.DynamicCopy) : this(usage)
    {
        Load(data, size);
    }
}

public class ShaderStorageBuffer : Buffer
{
    public override BufferTarget Target => BufferTarget.ShaderStorageBuffer;

    public ShaderStorageBuffer(BufferUsageHint usage = BufferUsageHint.DynamicCopy) : base(usage)
    {

    }

    public ShaderStorageBuffer(int size, BufferUsageHint usage = BufferUsageHint.DynamicCopy) : this(usage)
    {
        Allocate(size);
    }

    public ShaderStorageBuffer(Array data, int size, BufferUsageHint usage = BufferUsageHint.DynamicCopy) : this(usage)
    {
        Load(data, size);
    }

    public ShaderStorageBuffer(IntPtr data, int size, BufferUsageHint usage = BufferUsageHint.DynamicCopy) : this(usage)
    {
        Load(data, size);
    }
}

public class VertexBuffer : Buffer
{
    public override BufferTarget Target => BufferTarget.ArrayBuffer;
    public Attribute[] Attributes { get; }
    public int Count { get; set; }


    public VertexBuffer(int count = 0, BufferUsageHint usage = BufferUsageHint.StaticDraw, params Attribute[] attributes) : base(usage)
    {
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

    public VertexBuffer(IntPtr data, int size, int count = 0, BufferUsageHint usage = BufferUsageHint.DynamicCopy, params Attribute[] attributes) : this(count, usage, attributes)
    {
        Load(data, size);
    }

    public void CreateLayout(int vao, int index)
    {
        int stride = Attributes.Select(attrib => attrib.Size).Sum();
        for (int i = 0, offset = 0; i < Attributes.Length; i++)
        {
            Attributes[i].Offset = offset;
            Attributes[i].Stride = stride;
            offset += Attributes[i].Size;
        }
        GL.VertexArrayVertexBuffer(vao, index, Handle, IntPtr.Zero, stride);
        foreach (var attrib in Attributes)
        {
            attrib.Load(vao, index);
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

        public virtual void Load(int vao, int index)
        {
            GL.EnableVertexArrayAttrib(vao, Index);
            GL.VertexArrayAttribBinding(vao, Index, index);
            GL.VertexArrayAttribFormat(vao, Index, Count, Type, Normalized, Offset);
        }
    }

    public class IntegerAttribute : Attribute
    {
        public IntegerAttribute(int index, int count, VertexAttribType type = VertexAttribType.Int, int stride = 0, int offset = 0) : base(index, count, type, false, stride, offset)
        {
        }

        public override void Load(int vao, int index)
        {
            GL.EnableVertexArrayAttrib(vao, Index);
            GL.VertexArrayAttribBinding(vao, Index, index);
            GL.VertexArrayAttribIFormat(vao, Index, Count, Type, Offset);
        }
    }
}