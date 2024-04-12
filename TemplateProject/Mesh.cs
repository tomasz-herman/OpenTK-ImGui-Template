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
        GL.DrawArrays(Type, 0, VertexBuffers[0].Count);
    }

    public void Render(int offset, int count)
    {
        GL.DrawArrays(Type, offset, count);
    }

    public void RenderIndexed()
    {
        if (IndexBuffer is null) throw new InvalidOperationException("Index Buffer is null");

        GL.DrawElements(Type, IndexBuffer.Count, IndexBuffer.ElementsType, 0);
    }

    public void RenderIndexed(int offset, int count, int vertexOffset = 0)
    {
        if (IndexBuffer is null) throw new InvalidOperationException("Index Buffer is null");

        if (vertexOffset == 0)
            GL.DrawElements(Type, count, IndexBuffer.ElementsType, offset);
        else
            GL.DrawElementsBaseVertex(Type, count, IndexBuffer.ElementsType, offset, vertexOffset);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(Handle);
        VertexBuffers.ForEach(buffer => buffer.Dispose());
        IndexBuffer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
