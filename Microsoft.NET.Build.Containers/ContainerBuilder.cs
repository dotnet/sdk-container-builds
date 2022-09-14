namespace Microsoft.NET.Build.Containers;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class ContainerBuilder
{
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