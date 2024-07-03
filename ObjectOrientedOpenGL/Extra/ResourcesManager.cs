using ObjectOrientedOpenGL.Core;

namespace ObjectOrientedOpenGL.Extra;

public class ResourcesManager
{
    public static ResourcesManager Instance { get; } = new();

    private Dictionary<string, Texture> Textures { get; } = new();
    private Dictionary<string, Model> Models { get; } = new();

    private ResourcesManager()
    {
        
    }

    public Texture GetTexture(string path)
    {
        if (Textures.TryGetValue(path, out var texture)) return texture;
        return Textures[path] = new Texture(path);
    }

    public Model GetModel(string path)
    {
        if (Models.TryGetValue(path, out var model)) return model;
        return Models[path] = ModelLoader.Load(path);
    }
}