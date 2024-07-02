using System.Reflection;
using System.Runtime.InteropServices;
using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;

namespace TemplateProject;

public static class ModelLoader
{
    public const string ResourcesPath = "TemplateProject.Resources.models";

    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Tangent;
        public Vector3 BiTangent;
        public Vector2 TextureCoordinate;
    }

    public static Model Load(string path, PostProcessSteps ppSteps = PostProcessSteps.Triangulate |
                                                                          PostProcessSteps.GenerateNormals |
                                                                          PostProcessSteps.JoinIdenticalVertices |
                                                                          PostProcessSteps.FixInFacingNormals)
    {
        AssimpContext context = new AssimpContext();
        var assembly = Assembly.GetAssembly(typeof(ModelLoader))!;
        using Stream? stream = assembly.GetManifestResourceStream($"{ResourcesPath}.{path}");
        Scene scene = context.ImportFileFromStream(stream, ppSteps, Path.GetExtension(path));

        List<Mesh> meshes = ProcessMeshes(scene);
        // List<Texture> textures = new List<Texture>();
        // List<Object> materials = new List<Object>();

        Model.Node root = ProcessNode(scene.RootNode, meshes);

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
                {
                    Position = mesh.Vertices[i].AsOpenTkVector(),
                    Normal = mesh.Normals[i].AsOpenTkVector(),
                    Tangent = mesh.Tangents[i].AsOpenTkVector(),
                    BiTangent = mesh.BiTangents[i].AsOpenTkVector(),
                    TextureCoordinate = mesh.TextureCoordinateChannels[0][i].AsOpenTkVector2()
                };
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