using ObjectOrientedOpenGL.Core;
using System.Reflection;
using System.Runtime.InteropServices;
using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using Mesh = ObjectOrientedOpenGL.Core.Mesh;

namespace ObjectOrientedOpenGL.Extra;

public static class ModelLoader
{
    public struct Vertex(
        Vector3 position,
        Vector3 normal,
        Vector3 tangent,
        Vector3 biTangent,
        Vector2 textureCoordinate)
    {
        public Vector3 Position = position;
        public Vector3 Normal = normal;
        public Vector3 Tangent = tangent;
        public Vector3 BiTangent = biTangent;
        public Vector2 TextureCoordinate = textureCoordinate;
    }

    public static Model Load(string path, PostProcessSteps ppSteps = PostProcessSteps.Triangulate |
                                                                          PostProcessSteps.GenerateNormals |
                                                                          PostProcessSteps.JoinIdenticalVertices |
                                                                          PostProcessSteps.FixInFacingNormals)
    {
        var context = new AssimpContext();
        using var stream = ResourcesUtils.GetResourceStream(path);
        var scene = context.ImportFileFromStream(stream, ppSteps, Path.GetExtension(path));

        var meshes = ProcessMeshes(scene);
        // List<Texture> textures = new List<Texture>();
        // List<Object> materials = new List<Object>();

        var root = ProcessNode(scene.RootNode, meshes);

        return new Model(path, meshes, root);
    }

    private static Model.Node ProcessNode(Node node, List<Mesh> meshes)
    { 
        return new Model.Node(
            node.Name, 
            node.Transform.AsOpenTkMatrix4(), 
            node.MeshIndices.Select(i => meshes[i]).ToList(), 
            node.Children.Select(child => ProcessNode(child, meshes)).ToList());
    }

    private static List<Mesh> ProcessMeshes(Scene scene)
    {
        var meshes = new List<Mesh>();
        foreach (var mesh in scene.Meshes)
        {
            Vertex[] vertices = new Vertex[mesh.VertexCount];
            uint[] indices = mesh.GetUnsignedIndices();
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                vertices[i] = new Vertex
                (
                    mesh.Vertices[i].AsOpenTkVector(),
                    mesh.Normals[i].AsOpenTkVector(),
                    mesh.Tangents[i].AsOpenTkVector(),
                    mesh.BiTangents[i].AsOpenTkVector(),
                    mesh.TextureCoordinateChannels[0][i].AsOpenTkVector2()
                );
            }

            var ibo = new IndexBuffer(indices, indices.Length * sizeof(uint), DrawElementsType.UnsignedInt, indices.Length);
            var vbo = new VertexBuffer(vertices, vertices.Length * Marshal.SizeOf<Vertex>(), vertices.Length);
            meshes.Add(new Mesh(mesh.Name, PrimitiveType.Triangles, ibo, vbo));
        }

        return meshes;
    }

    private static Vector3 AsOpenTkVector(this Vector3D vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
    }
    
    private static Vector2 AsOpenTkVector2(this Vector3D vector)
    {
        return new Vector2(vector.X, vector.Y);
    }
    
    private static Matrix4 AsOpenTkMatrix4(this Matrix4x4 matrix)
    {
        return new Matrix4(
            new Vector4(matrix.A1, matrix.A2, matrix.A3, matrix.A4),
            new Vector4(matrix.B1, matrix.B2, matrix.B3, matrix.B4),
            new Vector4(matrix.C1, matrix.C2, matrix.C3, matrix.C4),
            new Vector4(matrix.D1, matrix.D2, matrix.D3, matrix.D4));
    }
}