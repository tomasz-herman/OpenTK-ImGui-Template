using ObjectOrientedOpenGL.Core;
using OpenTK.Mathematics;

namespace ObjectOrientedOpenGL.Graphics;

/// <summary>
/// A single draw primitive pairing GPU geometry (<see cref="Mesh"/>) with a <see cref="PbrMaterial"/>.
/// </summary>
public sealed class MeshPrimitive(string name, Mesh mesh, PbrMaterial material, BoundingBox bounds)
{
    public string Name { get; } = name;
    public Mesh Mesh { get; } = mesh;
    public PbrMaterial Material { get; } = material;
    public BoundingBox Bounds { get; } = bounds;
}

/// <summary>
/// Hierarchical node inside a loaded <see cref="Model"/>.
/// </summary>
public sealed class ModelNode
{
    public string Name { get; }
    public Matrix4 LocalTransform { get; set; }
    public ModelNode? Parent { get; private set; }
    public IReadOnlyList<MeshPrimitive> Primitives { get; }
    public IReadOnlyList<ModelNode> Children { get; }

    public ModelNode(string name, Matrix4 localTransform, List<MeshPrimitive> primitives, List<ModelNode> children)
    {
        Name = name;
        LocalTransform = localTransform;
        Primitives = primitives;
        Children = children;
        foreach (var child in children)
            child.Parent = this;
    }

    public Matrix4 GetWorldTransform()
    {
        return Parent is null ? LocalTransform : LocalTransform * Parent.GetWorldTransform();
    }
}

/// <summary>
/// Complete 3D model asset owning GPU meshes, textures, materials, and a hierarchical node graph.
/// </summary>
public sealed class Model : IDisposable
{
    public string Path { get; }
    public ModelNode Root { get; }
    public IReadOnlyList<MeshPrimitive> Primitives { get; }
    public IReadOnlyList<PbrMaterial> Materials { get; }
    public IReadOnlyList<Texture> Textures { get; }
    public BoundingBox Bounds { get; }

    public Model(
        string path,
        ModelNode root,
        List<MeshPrimitive> primitives,
        List<PbrMaterial> materials,
        List<Texture> textures)
    {
        Path = path;
        Root = root;
        Primitives = primitives;
        Materials = materials;
        Textures = textures;
        Bounds = ComputeBounds(root, Matrix4.Identity);
    }

    private static BoundingBox ComputeBounds(ModelNode node, Matrix4 parentWorld)
    {
        var world = node.LocalTransform * parentWorld;
        var box = BoundingBox.Empty;
        foreach (var prim in node.Primitives)
            box.Expand(prim.Bounds.Transform(world));
        foreach (var child in node.Children)
            box.Expand(ComputeBounds(child, world));
        return box;
    }

    public void Dispose()
    {
        foreach (var prim in Primitives)
            prim.Mesh.Dispose();
        foreach (var tex in Textures)
            tex.Dispose();
        GC.SuppressFinalize(this);
    }
}
