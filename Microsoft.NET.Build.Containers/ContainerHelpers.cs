namespace Microsoft.NET.Build.Containers;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class ContainerHelpers
{
    private static Regex imageTagRegex = new Regex(@"^[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}$");

    private static Regex imageNameRegex = new Regex(@"^[a-z0-9]+([._-][a-z0-9]+)*(/[a-z0-9]+([._-][a-z0-9]+)*)*$");

    /// <summary>
    /// Matches if the string is not lowercase or numeric, or ., _, or -.
    /// </summary>
    private static Regex imageNameCharacters = new Regex(@"[^a-z0-9._\-/]");

    /// <summary>
    /// Given some "fully qualified" image name (e.g. mcr.microsoft.com/dotnet/runtime), return
    /// a valid UriBuilder. This means appending 'https' if the URI is not absolute, otherwise UriBuilder will throw.
    /// </summary>
    /// <param name="containerBase"></param>
    /// <returns>A <see cref="Uri" /> with the given containerBase, or, if containerBase is relative, https:// + containerBase</returns>
    private static Uri? ContainerImageToUri(string containerBase)
    {
        Uri uri = new Uri(containerBase, UriKind.RelativeOrAbsolute);

        try
        {
            return uri.IsAbsoluteUri ? uri : new Uri(containerBase.Contains("localhost") ? "http://" : "https://" + uri);
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed parsing the container image into a UriBuilder: {0}", e);
            return null;
        }
    }

    /// <summary>
    /// Ensures the given registry is valid.
    /// </summary>
    /// <param name="imageName"></param>
    /// <returns></returns>
    public static bool IsValidRegistry(string registryName)
    {
        // No scheme prefixed onto the registry
        if (string.IsNullOrEmpty(registryName) ||
            (!registryName.StartsWith("http://") &&
             !registryName.StartsWith("https://") &&
             !registryName.StartsWith("docker://")))
        {
            return false;
        }

        try
        {
            UriBuilder uri = new UriBuilder(registryName);
        }
        catch
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures the given image name is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageName"></param>
    /// <returns></returns>
    public static bool IsValidImageName(string imageName)
    {
        return imageNameRegex.IsMatch(imageName);
    }

    /// <summary>
    /// Ensures the given tag is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageTag"></param>
    /// <returns></returns>
    public static bool IsValidImageTag(string imageTag)
    {
        return imageTagRegex.IsMatch(imageTag);
    }

    /// <summary>
    /// Parse a fully qualified container name (e.g. https://mcr.microsoft.com/dotnet/runtime:6.0)
    /// Note: Tag not required.
    /// </summary>
    /// <param name="fullyQualifiedContainerName"></param>
    /// <param name="containerRegistry"></param>
    /// <param name="containerName"></param>
    /// <param name="containerTag"></param>
    /// <returns>True if the parse was successful. When false is returned, all out vars are set to empty strings.</returns>
    public static bool TryParseFullyQualifiedContainerName(string fullyQualifiedContainerName,
                                                            [NotNullWhen(true)] out string? containerRegistry,
                                                            [NotNullWhen(true)] out string? containerName,
                                                            [NotNullWhen(true)] out string? containerTag)
    {
        Uri? uri = ContainerImageToUri(fullyQualifiedContainerName);

        if (uri == null || uri.Segments.Length <= 1)
        {
            containerRegistry = null;
            containerName = null;
            containerTag = null;
            return false;
        }

        // The first segment is the '/', create a string out of everything after.
        string image = uri.PathAndQuery.Substring(1);

        // If the image has a ':', there's a tag we need to parse.
        int indexOfColon = image.IndexOf(':');

        containerRegistry = uri.Scheme + "://" + uri.Host + (uri.Port > 0 && !uri.IsDefaultPort ? ":" + uri.Port : "");
        containerName = indexOfColon == -1 ? image : image.Substring(0, indexOfColon);
        containerTag = indexOfColon == -1 ? "" : image.Substring(indexOfColon + 1);
        return true;
    }

    /// <summary>
    /// Checks if a given container image name adheres to the image name spec. If not, and recoverable, then normalizes invalid characters.
    /// </summary>
    public static bool NormalizeImageName(string containerImageName, [NotNullWhen(false)] out string? normalizedImageName)
    {
        if (IsValidImageName(containerImageName))
        {
            normalizedImageName = null;
            return true;
        }
        else
        {
            if (!Char.IsLetterOrDigit(containerImageName, 0))
            {
                throw new ArgumentException("The first character of the image name must be a lowercase letter or a digit.");
            }
            var loweredImageName = containerImageName.ToLowerInvariant();
            normalizedImageName = imageNameCharacters.Replace(loweredImageName, "-");
            return false;
        }
    }

    [Flags]
    public enum ParsePortError
    {
        MissingPortNumber,
        InvalidPortNumber,
        InvalidPortType,
        UnknownPortFormat
    }

    public static bool TryParsePort(string? portNumber, string? portType, [NotNullWhen(true)] out Port? port, [NotNullWhen(false)] out ParsePortError? error)
    {
        var portNo = 0;
        error = null;
        if (String.IsNullOrEmpty(portNumber))
        {
            error = ParsePortError.MissingPortNumber;
        }
        else if (!int.TryParse(portNumber, out portNo))
        {
            error = ParsePortError.InvalidPortNumber;
        }

        if (!Enum.TryParse<PortType>(portType, out PortType t))
        {
            if (portType is not null)
            {
                error = (error ?? ParsePortError.InvalidPortType) | ParsePortError.InvalidPortType;
            }
            else
            {
                t = PortType.tcp;
            }
        }

        if (error is null)
        {
            port = new Port(portNo, t);
            return true;
        }
        else
        {
            port = null;
            return false;
        }

    }

    public static bool TryParsePort(string input, [NotNullWhen(true)] out Port? port, [NotNullWhen(false)] out ParsePortError? error)
    {
        var parts = input.Split('/');
        if (parts is [var portNumber, var type])
        {
            return TryParsePort(portNumber, type, out port, out error);
        }
        else if (parts is [var portNo])
        {
            return TryParsePort(portNo, null, out port, out error);
        }
        else
        {
            error = ParsePortError.UnknownPortFormat;
            port = null;
            return false;
        }
    }

    public static async Task Containerize(DirectoryInfo folder, string workingDir, string registryName, string baseName, string baseTag, string[] entrypoint, string[] entrypointArgs, string imageName, string[] imageTags, string outputRegistry, string[] labels, Port[] exposedPorts)
    {
        Registry baseRegistry = new Registry(new Uri(registryName));

        Console.WriteLine($"Reading from {baseRegistry.BaseUri}");

        Image img = await baseRegistry.GetImageManifest(baseName, baseTag);
        img.WorkingDirectory = workingDir;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };

        Console.WriteLine($"Copying from {folder.FullName} to {workingDir}");
        Layer l = Layer.FromDirectory(folder.FullName, workingDir);

        img.AddLayer(l);

        img.SetEntrypoint(entrypoint, entrypointArgs);

        var isDockerPush = outputRegistry.StartsWith("docker://");
        Registry? outputReg = isDockerPush ? null : new Registry(new Uri(outputRegistry));

        foreach (var label in labels)
        {
            string[] labelPieces = label.Split('=');

            // labels are validated by System.CommandLine API
            img.Label(labelPieces[0], labelPieces[1]);
        }

        foreach (var (number, type) in exposedPorts)
        {
            // ports are validated by System.CommandLine API
            img.ExposePort(number, type);
        }

        foreach (var tag in imageTags)
        {
            if (isDockerPush)
            {
                try
                {
                    LocalDocker.Load(img, imageName, tag, baseName).Wait();
                    Console.WriteLine("Pushed container '{0}:{1}' to Docker daemon", imageName, tag);
                }
                catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
                {
                    Console.WriteLine(dle);
                    Environment.ExitCode = -1;
                }
            }
            else
            {
                try
                {
                    Console.WriteLine($"Trying to push container '{imageName}:{tag}' to registry '{outputRegistry}'");
                    outputReg?.Push(img, imageName, tag, imageName).Wait();
                    Console.WriteLine($"Pushed container '{imageName}:{tag}' to registry '{outputRegistry}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to push to output registry: {0}", e);
                    Environment.ExitCode = -1;
                }
            }
        }
    }
}
