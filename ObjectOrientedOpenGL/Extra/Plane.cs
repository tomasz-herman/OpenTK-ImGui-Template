using System.Runtime.InteropServices;
using ObjectOrientedOpenGL.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Extra;

public class Plane : IDisposable
{
    private Mesh Mesh { get; }
    private Shader Shader { get; }
    private Texture Texture { get; }

    public Plane()
    {
        Vector4[] vertices =
        [
            new(0, 0, 0, 1),
            new(1, 0, 0, 0),
            new(0, 0, 1, 0),
            new(-1, 0, 0, 0),
            new(0, 0, -1, 0)
        ];
        byte[] indices = {
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 1
        };
        IndexBuffer ibo = new IndexBuffer(indices, indices.Length * sizeof(byte), DrawElementsType.UnsignedByte,
            indices.Length);
        VertexBuffer vbo = new VertexBuffer(vertices, vertices.Length * Marshal.SizeOf<Vector4>(), vertices.Length,
            BufferUsageHint.StaticDraw, new VertexBuffer.Attribute(0, 4));
        Mesh = new Mesh("Plane", PrimitiveType.Triangles, ibo, vbo);
        Shader = new Shader(("ObjectOrientedOpenGL.Resources.Shaders.plane.frag", ShaderType.FragmentShader),
            ("ObjectOrientedOpenGL.Resources.Shaders.plane.vert", ShaderType.VertexShader));
        Texture = new Texture("ObjectOrientedOpenGL.Resources.Textures.grid.png");
    }

    public void Render(Camera camera)
    {
        Shader.Use();
        Shader.LoadMatrix4("mvp", camera.ProjectionViewMatrix);
        var viewport = new int[4];
        GL.GetInteger(GetPName.Viewport, viewport);
        Shader.LoadFloat4("viewport", new Vector4(viewport[0], viewport[1], viewport[2], viewport[3]));
        Shader.LoadMatrix4("invViewProj", camera.ProjectionViewMatrix.Inverted());
        Shader.LoadInteger("sampler", 0);

        Texture.ActivateUnit();

        Mesh.Bind();
        Mesh.RenderIndexed();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Mesh.Dispose();
        Shader.Dispose();
        Texture.Dispose();
    }
}