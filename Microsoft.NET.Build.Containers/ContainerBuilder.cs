// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class ContainerBuilder
{
    public static async Task Containerize(DirectoryInfo folder, string workingDir, string registryName, string baseName, string baseTag, string[] entrypoint, string[] entrypointArgs, string imageName, string[] imageTags, string? outputRegistry, string[] labels, Port[] exposedPorts, string[] envVars, string containerRuntimeIdentifier, string ridGraphPath)
    {
        var isDockerPull = String.IsNullOrEmpty(registryName);
        if (isDockerPull) {
            throw new ArgumentException("Don't know how to pull images from local daemons at the moment");
        }
        Registry baseRegistry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));

        var img = await baseRegistry.GetImageManifest(baseName, baseTag, containerRuntimeIdentifier, ridGraphPath).ConfigureAwait(false);
        if (img is null) {
            throw new ArgumentException($"Could not find image {baseName}:{baseTag} in registry {registryName} matching RuntimeIdentifier {containerRuntimeIdentifier}");
        }

        img.WorkingDirectory = workingDir;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };

        Layer l = Layer.FromDirectory(folder.FullName, workingDir);

        img.AddLayer(l);

        img.SetEntrypoint(entrypoint, entrypointArgs);

        var isDockerPush = String.IsNullOrEmpty(outputRegistry);
        Registry? outputReg = isDockerPush ? null : new Registry(ContainerHelpers.TryExpandRegistryToUri(outputRegistry!));

        foreach (var label in labels)
        {
            string[] labelPieces = label.Split('=');

            // labels are validated by System.CommandLine API
            img.Label(labelPieces[0], labelPieces[1]);
        }

        foreach (string envVar in envVars)
        {
            string[] envPieces = envVar.Split('=', 2);

            img.AddEnvironmentVariable(envPieces[0], envPieces[1]);
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
                    Console.WriteLine("Containerize: Pushed container '{0}:{1}' to Docker daemon", imageName, tag);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Containerize: error CONTAINER001: Failed to push to local docker registry: {e}");
                    Environment.ExitCode = -1;
                }
            }
            else
            {
                try
                {
                    outputReg?.Push(img, imageName, tag, imageName, (message) => Console.WriteLine($"Containerize: {message}")).Wait();
                    Console.WriteLine($"Containerize: Pushed container '{imageName}:{tag}' to registry '{outputRegistry}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Containerize: error CONTAINER001: Failed to push to output registry: {e}");
                    Environment.ExitCode = -1;
                }
            }
        }
    }
}