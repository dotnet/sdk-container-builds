// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.NET.Build.Containers.Tasks;

namespace Microsoft.NET.Build.Containers;

public static class ContainerBuilder
{
    public static async Task ContainerizeAsync(
        DirectoryInfo folder,
        string workingDir,
        string registryName,
        string baseName,
        string baseTag,
        string[] entrypoint,
        string[] entrypointArgs,
        string imageName,
        string[] imageTags,
        string? outputRegistry,
        string[] labels,
        Port[] exposedPorts,
        string[] envVars,
        string containerRuntimeIdentifier,
        string ridGraphPath,
        string localContainerDaemon,
        string? tarOutputDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var isDaemonPull = String.IsNullOrEmpty(registryName);
        if (isDaemonPull)
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.DontKnowHowToPullImages)));
        }

        Registry baseRegistry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));
        ImageReference sourceImageReference = new(baseRegistry, baseName, baseTag);
        var outputMode = CreateNewImage.DeriveOutputMode(outputRegistry, tarOutputDirectory);
        var destinationRegistry = CreateNewImage.BuildDestinationRegistry(outputMode, outputRegistry);
        var destinationImageReferences = imageTags.Select(t => new ImageReference(destinationRegistry.Value, imageName, t));

        ImageBuilder imageBuilder = await baseRegistry.GetImageManifestAsync(baseName, baseTag, containerRuntimeIdentifier, ridGraphPath, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        imageBuilder.SetWorkingDirectory(workingDir);

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };

        Layer l = Layer.FromDirectory(folder.FullName, workingDir);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntryPoint(entrypoint, entrypointArgs);

        foreach (string label in labels)
        {
            string[] labelPieces = label.Split('=');

            // labels are validated by System.CommandLine API
            imageBuilder.AddLabel(labelPieces[0], TryUnquote(labelPieces[1]));
        }

        foreach (string envVar in envVars)
        {
            string[] envPieces = envVar.Split('=', 2);

            imageBuilder.AddEnvironmentVariable(envPieces[0], TryUnquote(envPieces[1]));
        }

        foreach ((int number, PortType type) in exposedPorts)
        {
            // ports are validated by System.CommandLine API
            imageBuilder.ExposePort(number, type);
        }

        BuiltImage builtImage = imageBuilder.Build();

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var destinationImageReference in destinationImageReferences)
        {
            switch (outputMode)
            {
                case CreateNewImage.ImageOutputMode.PushLocalDaemon:
                    var localDaemon = GetLocalDaemon(localContainerDaemon, Console.WriteLine);
                    if (!(await localDaemon.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
                    {
                        Console.WriteLine("Containerize: error CONTAINER007: The Docker daemon is not available, but pushing to a local daemon was requested. Please start Docker and try again.");
                        Environment.ExitCode = 7;
                        return;
                    }
                    try
                    {
                        await localDaemon.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
                        Console.WriteLine("Containerize: Pushed container '{0}' to Docker daemon", destinationImageReference.RepositoryAndTag);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Containerize: error CONTAINER001: Failed to push to local docker registry: {e.Message}");
                        Environment.ExitCode = 1;
                    }
                    break;
                case CreateNewImage.ImageOutputMode.PushRemoteRegistry:
                    try
                    {
                        await destinationImageReference.Registry!.PushAsync(
                            builtImage,
                            sourceImageReference,
                            destinationImageReference,
                            (message) => Console.WriteLine($"Containerize: {message}"),
                            cancellationToken).ConfigureAwait(false);
                        Console.WriteLine($"Containerize: Pushed container '{destinationImageReference.RepositoryAndTag}' to registry '{outputRegistry}'");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Containerize: error CONTAINER001: Failed to push to output registry: {e.Message}");
                        Environment.ExitCode = 1;
                    }
                    break;
                case CreateNewImage.ImageOutputMode.WriteTars:
                    try
                    {
                        var tarFileName = $"{imageName}-{destinationImageReference.RepositoryAndTag}.tar.gz";
                        var tarFilePath = Path.GetFullPath(Path.Combine(tarOutputDirectory!, tarFileName));

                        using var fileStream = File.Create(tarFilePath);
                        await LocalDocker.WriteImageToStreamAsync(builtImage, sourceImageReference, destinationImageReference, fileStream, cancellationToken).ConfigureAwait((false));
                        Console.WriteLine("Written image '{0}' to path '{1}'", destinationImageReference.RepositoryAndTag, tarFilePath);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Containerize: error CONTAINER001: Failed to write to output tar: {e.Message}");
                        Environment.ExitCode = 1;
                    }
                    break;
            }
        }
    }

    private static LocalDocker GetLocalDaemon(string localDaemonType, Action<string> logger)
    {
        var daemon = localDaemonType switch
        {
            KnownDaemonTypes.Docker => new LocalDocker(logger),
            _ => throw new ArgumentException($"Unknown local container daemon type '{localDaemonType}'. Valid local container daemon types are {String.Join(",", KnownDaemonTypes.SupportedLocalDaemonTypes)}", nameof(localDaemonType))
        };
        return daemon;
    }

    private static string TryUnquote(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 2)
        {
            return path;
        }
        if ((path[0] == '\"' && path[path.Length - 1] == '\"'))
        {
            return path.Substring(1, path.Length - 2);
        }

        //not quoted
        return path;
    }
}
