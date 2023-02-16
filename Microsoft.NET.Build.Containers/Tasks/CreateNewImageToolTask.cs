// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Containers.Tasks;

/// <summary>
/// This task will shell out to the net7.0-targeted application for VS scenarios.
/// </summary>
public partial class CreateNewImage : ToolTask, ICancelableTask
{
    // Unused, ToolExe is set via targets and overrides this.
    protected override string ToolName => "dotnet";

    private (bool success, string user, string pass) extractionInfo;

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

    protected override string GenerateFullPathToTool() => Quote(Path.Combine(DotNetPath, ToolExe));

    /// <summary>
    /// Workaround to avoid storing user/pass into the EnvironmentVariables property, which gets logged by the task.
    /// </summary>
    /// <param name="pathToTool"></param>
    /// <param name="commandLineCommands"></param>
    /// <param name="responseFileSwitch"></param>
    /// <returns></returns>
    protected override ProcessStartInfo GetProcessStartInfo(string pathToTool, string commandLineCommands, string responseFileSwitch)
    {
        VSHostObject hostObj = new VSHostObject(HostObject as System.Collections.Generic.IEnumerable<ITaskItem>);
        if (hostObj.ExtractCredentials(out string user, out string pass, (string s) => Log.LogWarning(s)))
        {
            extractionInfo = (true, user, pass);
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, "No host object detected.");
        }

        ProcessStartInfo startInfo = base.GetProcessStartInfo(pathToTool, commandLineCommands, responseFileSwitch)!;

        if (extractionInfo.success)
        {
            startInfo.Environment[ContainerHelpers.HostObjectUser] = extractionInfo.user;
            startInfo.Environment[ContainerHelpers.HostObjectPass] = extractionInfo.pass;
        }

        return startInfo;
    }

    protected override string GenerateCommandLineCommands() => GenerateCommandLineCommandsInt();

    /// <remarks>
    /// For unit test purposes
    /// </remarks>
    internal string GenerateCommandLineCommandsInt()
    {
        if (string.IsNullOrWhiteSpace(PublishDirectory))
        {
            throw new InvalidOperationException($"Required task property '{nameof(PublishDirectory)}' was not set or empty.");
        }
        if (string.IsNullOrWhiteSpace(BaseRegistry))
        {
            throw new InvalidOperationException($"Required task property '{nameof(BaseRegistry)}' was not set or empty.");
        }
        if (string.IsNullOrWhiteSpace(BaseImageName))
        {
            throw new InvalidOperationException($"Required task property '{nameof(BaseImageName)}' was not set or empty.");
        }
        if (string.IsNullOrWhiteSpace(ImageName))
        {
            throw new InvalidOperationException($"Required task property '{nameof(ImageName)}' was not set or empty.");
        }
        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            throw new InvalidOperationException($"Required task property '{nameof(WorkingDirectory)}' was not set or empty.");
        }
        if (Entrypoint.Length == 0)
        {
            throw new InvalidOperationException($"Required task property '{nameof(Entrypoint)}' was not set or empty.");
        }
        if (Entrypoint.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            throw new InvalidOperationException($"Required task property '{nameof(Entrypoint)}' contains empty items.");
        }

        var sanitizedEntryPoints = Entrypoint.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec));
        var sanitizedEntryPointArgs = EntrypointArgs.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec));

        return Quote(ContainerizeDirectory + "containerize.dll") + " " +
               Quote(PublishDirectory.TrimEnd(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar })) +
               " --baseregistry " + BaseRegistry +
               " --baseimagename " + BaseImageName +
               (!string.IsNullOrWhiteSpace(BaseImageTag) ? " --baseimagetag " + BaseImageTag : "") +
               (!string.IsNullOrWhiteSpace(OutputRegistry) ? " --outputregistry " + OutputRegistry : "") +
               "--localcontainerdaemon " + LocalContainerDaemon +
               " --imagename " + ImageName +
               " --workingdirectory " + WorkingDirectory +
               (Entrypoint.Length > 0 ? " --entrypoint " + String.Join(" ", sanitizedEntryPoints.Select((i) => i.ItemSpec)) : "") +
               (Labels.Length > 0 ? " --labels " + String.Join(" ", Labels.Select((i) => i.ItemSpec + "=" + Quote(i.GetMetadata("Value")))) : "") +
               (ImageTags.Length > 0 ? " --imagetags " + String.Join(" ", ImageTags.Select((i) => Quote(i))) : "") +
               (EntrypointArgs.Length > 0 ? " --entrypointargs " + String.Join(" ", sanitizedEntryPointArgs.Select((i) => i.ItemSpec)) : "") +
               (ExposedPorts.Length > 0 ? " --ports " + String.Join(" ", ExposedPorts.Select((i) => i.ItemSpec + "/" + i.GetMetadata("Type"))) : "") +
               (ContainerEnvironmentVariables.Length > 0 ? " --environmentvariables " + String.Join(" ", ContainerEnvironmentVariables.Select((i) => i.ItemSpec + "=" + Quote(i.GetMetadata("Value")))) : "") +
               (!string.IsNullOrWhiteSpace(ContainerRuntimeIdentifier) ? " --rid " + ContainerRuntimeIdentifier : "") +
               (!string.IsNullOrWhiteSpace(RuntimeIdentifierGraphPath) ? " --ridgraphpath " + RuntimeIdentifierGraphPath : "");
    }


    private static string Quote(string path)
    {
        if (string.IsNullOrEmpty(path) || (path[0] == '\"' && path[path.Length - 1] == '\"'))
        {
            // it's already quoted
            return path;
        }

        return $"\"{path}\"";
    }
}
