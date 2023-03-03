// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateNewImage : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolExe { get; set; }

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolPath { get; set; }

    /// <summary>
    /// Lists all kinds of output modes for the images after creation.
    /// </summary>
    internal enum ImageOutputMode
    {
        /// <summary>
        /// The images will be pushed into the configured local daemon
        /// </summary>
        PushLocalDaemon,
        /// <summary>
        /// The images will be pushed into a remote registry
        /// </summary>
        PushRemoteRegistry,
        /// <summary>
        /// The images will be written into tar.gz files.
        /// </summary>
        WriteTars
    }

    /// <summary>
    /// Derives the output mode using the given settings.
    /// </summary>
    /// <param name="outputRegistry">The configured output registry.</param>
    /// <param name="tarOutputDirectory">The configured output directory for tar.gz files.</param>
    /// <returns>The output mode to use for producing container images.</returns>
    internal static ImageOutputMode DeriveOutputMode(string? outputRegistry, string? tarOutputDirectory)
    {
        if (!string.IsNullOrEmpty(outputRegistry))
        {
            return ImageOutputMode.PushRemoteRegistry;
        }
        else if (!string.IsNullOrEmpty(tarOutputDirectory))
        {
            return ImageOutputMode.WriteTars;
        }
        else
        {
            return ImageOutputMode.PushLocalDaemon;
        }
    }

    /// <summary>
    /// Creates a Registry configuration based on the provided output mode and registry configuration.
    /// </summary>
    /// <param name="outputMode">The output mode,</param>
    /// <param name="outputRegistry">The output registry.</param>
    /// <returns>A lazy initialized <see cref="Registry"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An invalid output mode was provided.</exception>
    internal static Lazy<Registry?> BuildDestinationRegistry(ImageOutputMode outputMode, string? outputRegistry)
    {
        switch (outputMode)
        {
            case ImageOutputMode.PushLocalDaemon:
            case ImageOutputMode.WriteTars:
                return new Lazy<Registry?>(() => null);
            case ImageOutputMode.PushRemoteRegistry:
                return new Lazy<Registry?>(() => new Registry(ContainerHelpers.TryExpandRegistryToUri(outputRegistry!)));
            default:
                throw new ArgumentOutOfRangeException(nameof(outputMode));
        }
    }

    private ImageOutputMode CurrentOutputMode => DeriveOutputMode(OutputRegistry, TarOutputDirectory);

    private bool IsDaemonPull => string.IsNullOrEmpty(BaseRegistry);

    public void Cancel() => _cancellationTokenSource.Cancel();

    public override bool Execute()
    {
        return Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token)).GetAwaiter().GetResult();
    }

    internal async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogError("{0} '{1}' does not exist", nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }
        ImageReference sourceImageReference = new(SourceRegistry.Value, BaseImageName, BaseImageTag);
        var destinationImageReferences = ImageTags.Select(t => new ImageReference(DestinationRegistry.Value, ImageName, t));

        ImageBuilder? imageBuilder;
        if (SourceRegistry.Value is { } registry)
        {
            imageBuilder = await registry.GetImageManifestAsync(
                BaseImageName,
                BaseImageTag,
                ContainerRuntimeIdentifier,
                RuntimeIdentifierGraphPath,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new NotSupportedException("Don't know how to pull images from local daemons at the moment");
        }

        if (imageBuilder is null)
        {
            Log.LogError($"Couldn't find matching base image for {0} that matches RuntimeIdentifier {1}", sourceImageReference.RepositoryAndTag, ContainerRuntimeIdentifier);
            return !Log.HasLoggedErrors;
        }

        SafeLog("Building image '{0}' with tags {1} on top of base image {2}", ImageName, String.Join(",", ImageTags), sourceImageReference);

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(WorkingDirectory);
        imageBuilder.SetEntryPoint(Entrypoint.Select(i => i.ItemSpec).ToArray(), EntrypointArgs.Select(i => i.ItemSpec).ToArray());

        foreach (ITaskItem label in Labels)
        {
            imageBuilder.AddLabel(label.ItemSpec, label.GetMetadata("Value"));
        }

        SetEnvironmentVariables(imageBuilder, ContainerEnvironmentVariables);

        SetPorts(imageBuilder, ExposedPorts);

        // at the end of this step, if any failed then bail out.
        if (Log.HasLoggedErrors)
        {
            return false;
        }

        BuiltImage builtImage = imageBuilder.Build();
        cancellationToken.ThrowIfCancellationRequested();

        // at this point we're done with modifications and are just pushing the data other places
        GeneratedContainerManifest = JsonSerializer.Serialize(builtImage.Manifest);
        GeneratedContainerConfiguration = builtImage.Config;

        ImageOutputMode outputMode = CurrentOutputMode;

        switch (outputMode)
        {
            case ImageOutputMode.PushLocalDaemon:
                if (!await PushImagesToLocalDaemonAsync(builtImage, sourceImageReference, destinationImageReferences, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
                break;
            case ImageOutputMode.PushRemoteRegistry:
                if (!await PushImagesToRemoteRegistryAsync(builtImage, sourceImageReference, destinationImageReferences, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
                break;
            case ImageOutputMode.WriteTars:
                if (!await WriteTarsAync(builtImage, sourceImageReference, destinationImageReferences, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
                break;
        }

        return !Log.HasLoggedErrors;
    }

    private async Task<bool> WriteTarsAync(BuiltImage builtImage, ImageReference sourceImageReference,
        IEnumerable<ImageReference> destinationImageReferences, CancellationToken cancellationToken)
    {
        List<ITaskItem> generatedTars = new();

        try
        {
            Directory.CreateDirectory(TarOutputDirectory);
        }
        catch (Exception e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }

            return false;
        }

        foreach (ImageReference destinationImageReference in destinationImageReferences)
        {
            try
            {
                string tarFileName = $"{ImageName}-{destinationImageReference.RepositoryAndTag}.tar.gz";
                string tarFilePath = Path.GetFullPath(Path.Combine(TarOutputDirectory, tarFileName));

                using FileStream fileStream = File.Create(tarFilePath);
                await LocalDocker.WriteImageToStreamAsync(builtImage, sourceImageReference, destinationImageReference,
                        fileStream, cancellationToken).ConfigureAwait(false);
                SafeLog("Written image '{0}' to path '{1}'", destinationImageReference.RepositoryAndTag, tarFilePath);
                generatedTars.Add(new TaskItem(tarFilePath));
            }
            catch (Exception e)
            {
                if (BuildEngine != null)
                {
                    Log.LogErrorFromException(e, true);
                }

                return false;
            }
        }

        GeneratedTars = generatedTars.ToArray();
        return true;
    }

    private async Task<bool> PushImagesToRemoteRegistryAsync(BuiltImage builtImage, ImageReference sourceImageReference,
        IEnumerable<ImageReference> destinationImageReferences, CancellationToken cancellationToken)
    {
        foreach (ImageReference destinationImageReference in destinationImageReferences)
        {
            try
            {
                if (destinationImageReference.Registry is not null)
                {
                    await (destinationImageReference.Registry.PushAsync(
                        builtImage,
                        sourceImageReference,
                        destinationImageReference,
                        message => SafeLog(message),
                        cancellationToken)).ConfigureAwait(false);
                    SafeLog("Pushed container '{0}' to registry '{2}'", destinationImageReference.RepositoryAndTag,
                        OutputRegistry);
                }
            }
            catch (ContainerHttpException e)
            {
                if (BuildEngine != null)
                {
                    Log.LogErrorFromException(e, true);
                }

                return false;
            }
            catch (Exception e)
            {
                if (BuildEngine != null)
                {
                    Log.LogError("Failed to push to the output registry: {0}", e);
                }

                return false;
            }
        }

        return true;
    }

    private async Task<bool> PushImagesToLocalDaemonAsync(BuiltImage builtImage, ImageReference sourceImageReference,
        IEnumerable<ImageReference> destinationImageReferences, CancellationToken cancellationToken)
    {
        foreach (ImageReference destinationImageReference in destinationImageReferences)
        {
            LocalDocker localDaemon = GetLocalDaemon(msg => Log.LogMessage(msg));
            if (!(await localDaemon.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
            {
                Log.LogError("The local daemon is not available, but pushing to a local daemon was requested. Please start the daemon and try again.");
                return false;
            }
            try
            {
                await localDaemon.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
                SafeLog("Pushed container '{0}' to local daemon", destinationImageReference.RepositoryAndTag);
            }
            catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
            {
                Log.LogErrorFromException(dle, showStackTrace: false);
                return false;
            }
        }

        return true;
    }

    private void SetPorts(ImageBuilder image, ITaskItem[] exposedPorts)
    {
        foreach (var port in exposedPorts)
        {
            var portNo = port.ItemSpec;
            var portType = port.GetMetadata("Type");
            if (ContainerHelpers.TryParsePort(portNo, portType, out Port? parsedPort, out ContainerHelpers.ParsePortError? errors))
            {
                image.ExposePort(parsedPort.Value.Number, parsedPort.Value.Type);
            }
            else
            {
                ContainerHelpers.ParsePortError parsedErrors = (ContainerHelpers.ParsePortError)errors!;
                var portString = portType == null ? portNo : $"{portNo}/{portType}";
                if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.MissingPortNumber))
                {
                    Log.LogError("ContainerPort item '{0}' does not specify the port number. Please ensure the item's Include is a port number, for example '<ContainerPort Include=\"80\" />'", port.ItemSpec);
                }
                else
                {
                    var message = "A ContainerPort item was provided with ";
                    var arguments = new List<string>(2);
                    if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber) && parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port number '{0}' and an invalid port type '{1}'";
                        arguments.Add(portNo);
                        arguments.Add(portType!);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port number '{0}'";
                        arguments.Add(portNo);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port type '{0}'";
                        arguments.Add(portType!);
                    }
                    message += ". ContainerPort items must have an Include value that is an integer, and a Type value that is either 'tcp' or 'udp'";

                    Log.LogError(message, arguments);
                }
            }
        }
    }

    private LocalDocker GetLocalDaemon(Action<string> logger)
    {
        var daemon = LocalContainerDaemon switch
        {
            KnownDaemonTypes.Docker => new LocalDocker(logger),
            _ => throw new NotSupportedException(
                Resource.FormatString(
                    nameof(Strings.UnknownDaemonType),
                    LocalContainerDaemon,
                    string.Join(",", KnownDaemonTypes.SupportedLocalDaemonTypes)))
        };
        return daemon;
    }

    private Lazy<Registry?> SourceRegistry
    {
        get
        {
            if(IsDaemonPull)
            {
                return new Lazy<Registry?>(() => null);
            }
            else
            {
                return new Lazy<Registry?>(() => new Registry(ContainerHelpers.TryExpandRegistryToUri(BaseRegistry)));
            }
        }
    }

    private Lazy<Registry?> DestinationRegistry => BuildDestinationRegistry(CurrentOutputMode, OutputRegistry);

    private static void SetEnvironmentVariables(ImageBuilder img, ITaskItem[] envVars)
    {
        foreach (ITaskItem envVar in envVars)
        {
            img.AddEnvironmentVariable(envVar.ItemSpec, envVar.GetMetadata("Value"));
        }
    }

    private void SafeLog(string message, params object[] formatParams) {
        if(BuildEngine != null) Log.LogMessage(MessageImportance.High, message, formatParams);
    }
}
