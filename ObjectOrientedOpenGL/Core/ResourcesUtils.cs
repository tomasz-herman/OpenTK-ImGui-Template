namespace ObjectOrientedOpenGL.Core;

public static class ResourcesUtils
{
    public static Stream? GetResourceStream(string path)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetManifestResourceStream(path))
            .OfType<Stream>()
            .FirstOrDefault();
    }
}