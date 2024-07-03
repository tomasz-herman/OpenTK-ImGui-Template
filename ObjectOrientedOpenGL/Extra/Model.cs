using ObjectOrientedOpenGL.Core;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Extra;

public class Model : IDisposable
{
    public string Path { get; }
    private List<Mesh> Meshes { get; }
    public Node Root { get; set; }

    public Model(string path, List<Mesh> meshes, Node root)
    {
        Path = path;
        Meshes = meshes;
        Root = root;
    }

    public class Node
    {
        public string Name { get; }
        public Matrix4 Transform { get; }
        public List<Mesh> Meshes { get; }
        public List<Node> Children { get; }

        public Node(string name, Matrix4 transform, List<Mesh> meshes, List<Node> children)
        {
            Name = name;
            Transform = transform;
            Meshes = meshes;
            Children = children;
        }
    }

    public void Dispose()
    {
        foreach (var mesh in Meshes)
        {
            mesh.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}