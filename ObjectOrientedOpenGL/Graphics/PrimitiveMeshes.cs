using System.Runtime.InteropServices;
using ObjectOrientedOpenGL.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Graphics;

/// <summary>
/// Procedural PBR-ready 3D meshes sharing <see cref="StandardVertex"/> layout.
/// </summary>
public static class PrimitiveMeshes
{
    public static Mesh CreateCube(float size = 1.0f, string name = "Cube")
    {
        float h = size * 0.5f;
        var verts = new List<StandardVertex>(24);
        var indices = new List<uint>(36);

        void AddFace(Vector3 normal, Vector3 tangent, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            uint start = (uint)verts.Count;
            var t = new Vector4(tangent, 1f);
            verts.Add(new StandardVertex(v0, normal, t, new Vector2(0, 0)));
            verts.Add(new StandardVertex(v1, normal, t, new Vector2(1, 0)));
            verts.Add(new StandardVertex(v2, normal, t, new Vector2(1, 1)));
            verts.Add(new StandardVertex(v3, normal, t, new Vector2(0, 1)));
            indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
        }

        AddFace(Vector3.UnitZ, Vector3.UnitX, new(-h, -h, h), new(h, -h, h), new(h, h, h), new(-h, h, h));
        AddFace(-Vector3.UnitZ, -Vector3.UnitX, new(h, -h, -h), new(-h, -h, -h), new(-h, h, -h), new(h, h, -h));
        AddFace(Vector3.UnitY, Vector3.UnitX, new(-h, h, h), new(h, h, h), new(h, h, -h), new(-h, h, -h));
        AddFace(-Vector3.UnitY, Vector3.UnitX, new(-h, -h, -h), new(h, -h, -h), new(h, -h, h), new(-h, -h, h));
        AddFace(Vector3.UnitX, -Vector3.UnitZ, new(h, -h, h), new(h, -h, -h), new(h, h, -h), new(h, h, h));
        AddFace(-Vector3.UnitX, Vector3.UnitZ, new(-h, -h, -h), new(-h, -h, h), new(-h, h, h), new(-h, h, -h));

        return BuildMesh(name, verts.ToArray(), indices.ToArray());
    }

    public static Mesh CreateSphere(float radius = 0.5f, int stacks = 24, int slices = 32, string name = "Sphere")
    {
        var verts = new List<StandardVertex>((stacks + 1) * (slices + 1));
        var indices = new List<uint>(stacks * slices * 6);

        for (int i = 0; i <= stacks; i++)
        {
            float v = (float)i / stacks;
            float phi = v * MathF.PI;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            for (int j = 0; j <= slices; j++)
            {
                float u = (float)j / slices;
                float theta = u * MathF.PI * 2f;
                float sinTheta = MathF.Sin(theta);
                float cosTheta = MathF.Cos(theta);

                var normal = new Vector3(cosTheta * sinPhi, cosPhi, sinTheta * sinPhi);
                var tangent = new Vector4(-sinTheta, 0f, cosTheta, 1f);
                verts.Add(new StandardVertex(normal * radius, normal, tangent, new Vector2(u, v)));
            }
        }

        int stride = slices + 1;
        for (int i = 0; i < stacks; i++)
        {
            for (int j = 0; j < slices; j++)
            {
                uint a = (uint)(i * stride + j);
                uint b = (uint)((i + 1) * stride + j);
                uint c = (uint)((i + 1) * stride + j + 1);
                uint d = (uint)(i * stride + j + 1);
                indices.AddRange([a, d, b, b, d, c]);
            }
        }

        return BuildMesh(name, verts.ToArray(), indices.ToArray());
    }

    public static Mesh CreateTorus(float majorRadius = 0.5f, float minorRadius = 0.18f, int majorSegments = 36, int minorSegments = 20, string name = "Torus")
    {
        var verts = new List<StandardVertex>((majorSegments + 1) * (minorSegments + 1));
        var indices = new List<uint>(majorSegments * minorSegments * 6);

        for (int i = 0; i <= majorSegments; i++)
        {
            float u = (float)i / majorSegments;
            float theta = u * MathF.PI * 2f;
            float cosT = MathF.Cos(theta);
            float sinT = MathF.Sin(theta);

            for (int j = 0; j <= minorSegments; j++)
            {
                float v = (float)j / minorSegments;
                float phi = v * MathF.PI * 2f;
                float cosP = MathF.Cos(phi);
                float sinP = MathF.Sin(phi);

                float r = majorRadius + minorRadius * cosP;
                var pos = new Vector3(r * cosT, minorRadius * sinP, r * sinT);
                var normal = new Vector3(cosP * cosT, sinP, cosP * sinT).Normalized();
                var tangent = new Vector4(-sinT, 0f, cosT, 1f);
                verts.Add(new StandardVertex(pos, normal, tangent, new Vector2(u, v)));
            }
        }

        int stride = minorSegments + 1;
        for (int i = 0; i < majorSegments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                uint a = (uint)(i * stride + j);
                uint b = (uint)((i + 1) * stride + j);
                uint c = (uint)((i + 1) * stride + j + 1);
                uint d = (uint)(i * stride + j + 1);
                indices.AddRange([a, b, d, b, c, d]);
            }
        }

        return BuildMesh(name, verts.ToArray(), indices.ToArray());
    }

    public static Mesh CreatePlane(float width = 1.0f, float depth = 1.0f, string name = "Plane")
    {
        float hw = width * 0.5f;
        float hd = depth * 0.5f;
        var n = Vector3.UnitY;
        var t = new Vector4(1, 0, 0, 1);

        StandardVertex[] verts =
        [
            new(new(-hw, 0,  hd), n, t, new(0, 0)),
            new(new( hw, 0,  hd), n, t, new(1, 0)),
            new(new( hw, 0, -hd), n, t, new(1, 1)),
            new(new(-hw, 0, -hd), n, t, new(0, 1)),
        ];
        uint[] indices = [0, 1, 2, 0, 2, 3];
        return BuildMesh(name, verts, indices);
    }

    private static Mesh BuildMesh(string name, StandardVertex[] vertices, uint[] indices)
    {
        var ibo = new IndexBuffer(indices, indices.Length * sizeof(uint), DrawElementsType.UnsignedInt, indices.Length);
        var vbo = new VertexBuffer(
            vertices,
            vertices.Length * Marshal.SizeOf<StandardVertex>(),
            vertices.Length,
            BufferUsageHint.StaticDraw,
            StandardVertex.Attributes);
        return new Mesh(name, PrimitiveType.Triangles, ibo, vbo);
    }
}
