namespace Microsoft.NET.Build.Containers.Tasks;

using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// This task will shell out to the net7.0-targeted application for VS scenarios.
/// </summary>
public class CreateNewImageToolTask : ToolTask
{
    /// <summary>
    /// The path to the folder containing `containerize.dll`.
    /// </summary>
    [Required]
    public string ContainerizeDirectory { get; set; }

    [Required]
    public string BaseRegistry { get; set; }

    /// <summary>
    /// The base image to pull.
    /// Ex: dotnet/runtime
    /// </summary>
    [Required]
    public string BaseImageName { get; set; }

    /// <summary>
    /// The base image tag.
    /// Ex: 6.0
    /// </summary>
    [Required]
    public string BaseImageTag { get; set; }

    /// <summary>
    /// The registry to push to.
    /// </summary>
    [Required]
    public string OutputRegistry { get; set; }

    /// <summary>
    /// The name of the output image that will be pushed to the registry.
    /// </summary>
    [Required]
    public string ImageName { get; set; }

    /// <summary>
    /// The tag to associate with the new image.
    /// </summary>
    public ITaskItem[] ImageTags { get; set; }

    /// <summary>
    /// The directory for the build outputs to be published.
    /// Constructed from "$(MSBuildProjectDirectory)\$(PublishDir)"
    /// </summary>
    [Required]
    public string PublishDirectory { get; set; }

    /// <summary>
    /// The working directory of the container.
    /// </summary>
    [Required]
    public string WorkingDirectory { get; set; }

    /// <summary>
    /// The entrypoint application of the container.
    /// </summary>
    [Required]
    public ITaskItem[] Entrypoint { get; set; }

    /// <summary>
    /// Arguments to pass alongside Entrypoint.
    /// </summary>
    public ITaskItem[] EntrypointArgs { get; set; }

    /// <summary>
    /// Labels that the image configuration will include in metadata
    /// </summary>
    public ITaskItem[] Labels { get; set; }

    protected override string ToolName { get; }


    private string DotNetPath
    {
        get
        {
            string path = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "";
            if (string.IsNullOrEmpty(path))
            {
                path = string.IsNullOrEmpty(ToolPath) ? "" : ToolPath;
            }

            return path;
        }
    }

    public CreateNewImageToolTask()
    {
        ContainerizeDirectory = "";
        ToolName = "";
        BaseRegistry = "";
        BaseImageName = "";
        BaseImageTag = "";
        OutputRegistry = "";
        ImageName = "";
        ImageTags = Array.Empty<ITaskItem>();
        PublishDirectory = "";
        WorkingDirectory = "";
        Entrypoint = Array.Empty<ITaskItem>();
        EntrypointArgs = Array.Empty<ITaskItem>();
        Labels = Array.Empty<ITaskItem>();
    }

    protected override string GenerateFullPathToTool() => DotNetPath + ToolName;

    protected override string GenerateCommandLineCommands()
    {
        return ContainerizeDirectory + "containerize.dll " +
               PublishDirectory +
               " --baseregistry " + BaseRegistry +
               " --baseimagename " + BaseImageName +
               " --baseimagetag " + BaseImageTag +
               " --outputregistry " + OutputRegistry +
               " --imagename " + ImageName +
               " --workingdirectory " + WorkingDirectory +
               (Entrypoint.Length > 0 ? " --entrypoint " + Entrypoint.Select((i) => i.ItemSpec).Aggregate((i, s) => s += i + " ") : "") +
               (Labels.Length > 0 ? " --labels " + Labels.Select((i) => i.ItemSpec + "=" + i.GetMetadata("Value")).Aggregate((i, s) => s += i + " ") : "") +
               (ImageTags.Length > 0 ? " --imagetags " + ImageTags.Select((i) => i.ItemSpec).Aggregate((i, s) => s += i + " ") : "") +
               (EntrypointArgs.Length > 0 ? " --entrypointargs " + EntrypointArgs.Select((i) => i.ItemSpec).Aggregate((i, s) => s += i + " ") : "");
    }
}