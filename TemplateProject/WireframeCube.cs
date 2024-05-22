using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TemplateProject;

public class WireframeCube : IDisposable
{
    public Matrix4 ModelMatrix { get; set; }
    public Mesh Mesh { get; }

    public WireframeCube()
    {
        Vertex[] vertices = {
            new(new Vector3(0.5f, 0.5f, 0.5f)),
            new(new Vector3(-0.5f, 0.5f, 0.5f)),
            new(new Vector3(0.5f, -0.5f, 0.5f)),
            new(new Vector3(0.5f, 0.5f, -0.5f)),
            new(new Vector3(-0.5f, -0.5f, 0.5f)),
            new(new Vector3(-0.5f, 0.5f, -0.5f)),
            new(new Vector3(0.5f, -0.5f, -0.5f)),
            new(new Vector3(-0.5f, -0.5f, -0.5f))
        };
        byte[] indices = {
            0, 1, 0, 2, 0, 3, 1, 4, 1, 5, 2, 4, 2, 6, 3, 5, 3, 6, 4, 7, 5, 7, 6, 7
        };
        var indexBuffer = new IndexBuffer(indices, indices.Length * sizeof(byte),
            DrawElementsType.UnsignedByte, indices.Length);
        var vertexBuffer = new VertexBuffer(vertices, vertices.Length * Marshal.SizeOf<Vertex>(),
            vertices.Length, BufferUsageHint.StaticDraw,
            new VertexBuffer.Attribute(0, 3) /*positions*/);
        Mesh = new Mesh(PrimitiveType.Lines, indexBuffer, vertexBuffer);
        ModelMatrix = Matrix4.Identity;
    }

    private struct Vertex
    {
        public Vector3 Position;

        public Vertex(Vector3 position)
        {
            Position = position;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}