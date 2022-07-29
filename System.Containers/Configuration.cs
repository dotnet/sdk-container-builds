namespace System.Containers;

public static class Configuration
{
    public static string ArtifactRoot { get; set; } = Path.Combine(Path.GetTempPath(), "Containers");
    public static string ContentRoot
    {
        get => Path.Combine(ArtifactRoot, "Content");
    }

    public static string TempPath
    {
        get
        {
            string tempPath = Path.Join(ArtifactRoot, "Temp");

            Directory.CreateDirectory(tempPath);

            return tempPath;
        }
    }

    public static string GetPathForHash(string contentHash)
    {
        return Path.Combine(ContentRoot, contentHash);
    }

    public static string GetTempFile()
    {
        return Path.Join(TempPath, Path.GetRandomFileName());
    }
}
