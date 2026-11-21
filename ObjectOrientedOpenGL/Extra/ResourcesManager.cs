using ObjectOrientedOpenGL.Core;
using ObjectOrientedOpenGL.Graphics;

namespace ObjectOrientedOpenGL.Extra;

public sealed class ResourcesManager : IDisposable
{
    public static ResourcesManager Instance { get; } = new();

    private Dictionary<string, Texture> Textures { get; } = new();
    private Dictionary<string, Model> Models { get; } = new();

    private ResourcesManager()
    {
    }

    public Texture GetTexture(string path, bool fromResource = true)
    {
        if (Textures.TryGetValue(path, out var texture)) return texture;
        return Textures[path] = new Texture(path, fromResource);
    }

    public Model GetModel(string path, bool fromResource = false)
    {
        if (Models.TryGetValue(path, out var model)) return model;
        return Models[path] = GltfModelLoader.Load(path, fromResource);
    }

    public void Dispose()
    {
        foreach (var tex in Textures.Values) tex.Dispose();
        foreach (var model in Models.Values) model.Dispose();
        Textures.Clear();
        Models.Clear();
    }
}
