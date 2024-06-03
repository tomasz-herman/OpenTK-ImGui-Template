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

    public static List<Mesh> Load(string path, PostProcessSteps ppSteps = PostProcessSteps.Triangulate |
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

        return meshes;
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
            meshes.Add(new Mesh(PrimitiveType.Triangles, ibo, vbo));
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
}