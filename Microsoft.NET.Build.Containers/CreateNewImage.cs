using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

public class CreateNewImage : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// The base registry to pull from.
    /// Ex: https://mcr.microsoft.com
    /// </summary>
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
    public string[] ImageTags { get; set; }

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

    public CreateNewImage()
    {
        BaseRegistry = "";
        BaseImageName = "";
        BaseImageTag = "";
        OutputRegistry = "";
        ImageName = "";
        ImageTags = Array.Empty<string>();
        PublishDirectory = "";
        WorkingDirectory = "";
        Entrypoint = Array.Empty<ITaskItem>();
        EntrypointArgs = Array.Empty<ITaskItem>();
        Labels = Array.Empty<ITaskItem>();
    }


    public override bool Execute()
    {
        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogError("{0} '{1}' does not exist", nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }

        if (BuildEngine != null)
        {
            Log.LogMessage($"Loading from directory: {PublishDirectory}");
        }

        string[] allLabels = new string[Labels.Length];
        for (int i = 0; i < Labels.Length; i++)
        {
            allLabels[i] = Labels[i].ItemSpec + "=" + Labels[i].GetMetadata("Value");
        }
        
        try
        {
            ContainerHelpers.Containerize(new DirectoryInfo(PublishDirectory),
                                          WorkingDirectory,
                                          BaseRegistry,
                                          BaseImageName,
                                          BaseImageTag,
                                          Entrypoint.Select((i) => i.ItemSpec).ToArray(),
                                          EntrypointArgs.Select((i) => i.ItemSpec).ToArray(),
                                          ImageName,
                                          ImageTags,
                                          OutputRegistry,
                                          allLabels).Wait();
        }
        catch (Exception e)
        {
            Log.LogErrorFromException(e);
        }

        return !Log.HasLoggedErrors;
    }
}