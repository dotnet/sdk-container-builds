using System.CommandLine;
using Microsoft.NET.Build.Containers;
using System.Text.Json;
using System.CommandLine.Parsing;

var publishDirectoryArg = new Argument<DirectoryInfo>(
    name: "PublishDirectory",
    description: "The directory for the build outputs to be published.")
    .LegalFilePathsOnly().ExistingOnly();

var baseRegistryOpt = new Option<string>(
    name: "--baseregistry",
    description: "The base registry to use")
{
    IsRequired = true
};

var baseImageNameOpt = new Option<string>(
    name: "--baseimagename",
    description: "The base image to pull.")
{
    IsRequired = true
};

// // Add validator here
var baseImageTagOpt = new Option<string>(
    name: "--baseimagetag",
    description: "The base image tag. Ex: 6.0",
    getDefaultValue: () => "latest");

var outputRegistryOpt = new Option<string>(
    name: "--outputregistry",
    description: "The registry to push to.")
{
    IsRequired = true
};

var imageNameOpt = new Option<string>(
    name: "--imagename",
    description: "The name of the output image that will be pushed to the registry.")
{
    IsRequired = true
};

var imageTagsOpt = new Option<string[]>(
    name: "--imagetags",
    description: "The tags to associate with the new image.");

var workingDirectoryOpt = new Option<string>(
    name: "--workingdirectory",
    description: "The working directory of the container.")
{
    IsRequired = true
};

var entrypointOpt = new Option<string[]>(
    name: "--entrypoint",
    description: "The entrypoint application of the container.")
{
    IsRequired = true
};

var entrypointArgsOpt = new Option<string[]>(
    name: "--entrypointargs",
    description: "Arguments to pass alongside Entrypoint.");

var labelsOpt = new Option<string[]>(
    name: "--labels",
    description: "Labels that the image configuration will include in metadata.",
    parseArgument: result =>
    {
        if (result.Tokens.Where((v) => v.Value.Split('=').Length != 2).Count() != 0)
        {
            result.ErrorMessage = "Incorrectly formatted label. Format: x=y";
            return new string[] { };
        }
        return result.Tokens.Select(v => v.Value).ToArray<string>();
    });

RootCommand root = new RootCommand("Containerize an application without Docker.")
{
    publishDirectoryArg,
    baseRegistryOpt,
    baseImageNameOpt,
    baseImageTagOpt,
    outputRegistryOpt,
    imageNameOpt,
    imageTagsOpt,
    workingDirectoryOpt,
    entrypointOpt,
    entrypointArgsOpt,
    labelsOpt
};

root.SetHandler(async (context) =>
{
    DirectoryInfo _publishDir = context.ParseResult.GetValueForArgument(publishDirectoryArg);
    string _baseReg = context.ParseResult.GetValueForOption(baseRegistryOpt) ?? "";
    string _baseName = context.ParseResult.GetValueForOption(baseImageNameOpt) ?? "";
    string _baseTag = context.ParseResult.GetValueForOption(baseImageTagOpt) ?? "";
    string _outputReg = context.ParseResult.GetValueForOption(outputRegistryOpt) ?? "";
    string _name = context.ParseResult.GetValueForOption(imageNameOpt) ?? "";
    string[] _tags = context.ParseResult.GetValueForOption(imageTagsOpt) ?? Array.Empty<string>();
    string _workingDir = context.ParseResult.GetValueForOption(workingDirectoryOpt) ?? "";
    string[] _entrypoint = context.ParseResult.GetValueForOption(entrypointOpt) ?? Array.Empty<string>();
    string[] _entrypointArgs = context.ParseResult.GetValueForOption(entrypointArgsOpt) ?? Array.Empty<string>();
    string[] _labels = context.ParseResult.GetValueForOption(labelsOpt) ?? Array.Empty<string>();

    await Containerize(_publishDir, _workingDir, _baseReg, _baseName, _baseTag, _entrypoint, _entrypointArgs, _name, _tags, _outputReg, _labels);
});

return await root.InvokeAsync(args);

async Task Containerize(DirectoryInfo folder, string workingDir, string registryName, string baseName, string baseTag, string[] entrypoint, string[] entrypointArgs, string imageName, string[] imageTags, string outputRegistry, string[] labels)
{
    Registry registry = new Registry(new Uri(registryName));

    Console.WriteLine($"Reading from {registry.BaseUri}");

    Image img = await registry.GetImageManifest(baseName, baseTag);
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

        // labels are validated by System.Commandline API
        img.Label(labelPieces[0], labelPieces[1]);
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
            }
        }
    }

    //Console.WriteLine($"Loaded image into local Docker daemon. Use 'docker run --rm -it --name {imageName} {registryName}/{imageName}:{imageTag}' to run the application.");
}