// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

public class ParseContainerProperties : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// The full base image name. mcr.microsoft.com/dotnet/runtime:6.0, for example.
    /// </summary>
    [Required]
    public string FullyQualifiedBaseImageName { get; set; }

    /// <summary>
    /// The registry to push the new container to. This will be null if the container is to be pushed to a local daemon.
    /// </summary>
    public string ContainerRegistry { get; set; }

    /// <summary>
    /// The image name for the container to be created.
    /// </summary>
    [Required]
    public string ContainerImageName { get; set; }

    /// <summary>
    /// The tag for the container to be created.
    /// </summary>
    public string ContainerImageTag { get; set; }
    /// <summary>
    /// The tags for the container to be created.
    /// </summary>
    public string[] ContainerImageTags { get; set; }

    /// <summary>
    /// Container environment variables to set.
    /// </summary>
    public ITaskItem[] ContainerEnvironmentVariables { get; set; }

    [Output]
    public string ParsedContainerRegistry { get; private set; }

    [Output]
    public string ParsedContainerImage { get; private set; }

    [Output]
    public string ParsedContainerTag { get; private set; }

    [Output]
    public string NewContainerRegistry { get; private set; }

    [Output]
    public string NewContainerImageName { get; private set; }

    [Output]
    public string[] NewContainerTags { get; private set; }

    [Output]
    public ITaskItem[] NewContainerEnvironmentVariables { get; private set; }

    public ParseContainerProperties()
    {
        FullyQualifiedBaseImageName = "";
        ContainerRegistry = "";
        ContainerImageName = "";
        ContainerImageTag = "";
        ContainerImageTags = Array.Empty<string>();
        ContainerEnvironmentVariables = Array.Empty<ITaskItem>();
        ParsedContainerRegistry = "";
        ParsedContainerImage = "";
        ParsedContainerTag = "";
        NewContainerRegistry = "";
        NewContainerImageName = "";
        NewContainerTags = Array.Empty<string>();
        NewContainerEnvironmentVariables = Array.Empty<ITaskItem>();
    }

    private static bool TryValidateTags(string[] inputTags, out string[] validTags, out string[] invalidTags)
    {
        var v = new List<string>();
        var i = new List<string>();
        foreach (var tag in inputTags)
        {
            if (ContainerHelpers.IsValidImageTag(tag))
            {
                v.Add(tag);
            }
            else
            {
                i.Add(tag);
            }
        }
        validTags = v.ToArray();
        invalidTags = i.ToArray();
        return invalidTags.Length == 0;
    }

    public override bool Execute()
    {
        string[] validTags;
        if (!String.IsNullOrEmpty(ContainerImageTag) && ContainerImageTags.Length >= 1)
        {
            Log.LogError(null, "CONTAINER005", "Container.AmbiguousTags", null, 0, 0, 0, 0, $"Both {nameof(ContainerImageTag)} and {nameof(ContainerImageTags)} were provided, but only one or the other is allowed.");
            return !Log.HasLoggedErrors;
        }

        if (!String.IsNullOrEmpty(ContainerImageTag))
        {
            if (ContainerHelpers.IsValidImageTag(ContainerImageTag))
            {
                validTags = new[] { ContainerImageTag };
            }
            else
            {
                validTags = Array.Empty<string>();
                Log.LogError(null, KnownStrings.ErrorCodes.CONTAINER004, "Container.InvalidTag", null, 0, 0, 0, 0, "Invalid {0} provided: {1}. Image tags must be alphanumeric, underscore, hyphen, or period.", nameof(ContainerImageTag), ContainerImageTag);
            }
        }
        else if (ContainerImageTags.Length != 0 && TryValidateTags(ContainerImageTags, out var valids, out var invalids))
        {
            validTags = valids;
            if (invalids.Any())
            {
                Log.LogError(null, KnownStrings.ErrorCodes.CONTAINER004, "Container.InvalidTag", null, 0, 0, 0, 0, "Invalid {0} provided: {1}. {0} must be a semicolon-delimited list of valid image tags. Image tags must be alphanumeric, underscore, hyphen, or period.", nameof(ContainerImageTags), String.Join(",", invalids));
                return !Log.HasLoggedErrors;
            }
        }
        else
        {
            validTags = Array.Empty<string>();
        }

        if (!String.IsNullOrEmpty(ContainerRegistry) && !ContainerHelpers.IsValidRegistry(ContainerRegistry))
        {
            Log.LogError("Could not recognize registry '{0}'.", ContainerRegistry);
            return !Log.HasLoggedErrors;
        }

        ValidateEnvironmentVariables();

        if (FullyQualifiedBaseImageName.Contains(' ') && BuildEngine != null)
        {
            Log.LogWarning($"{nameof(FullyQualifiedBaseImageName)} had spaces in it, replacing with dashes.");
        }
        FullyQualifiedBaseImageName = FullyQualifiedBaseImageName.Replace(' ', '-');

        if (!ContainerHelpers.TryParseFullyQualifiedContainerName(FullyQualifiedBaseImageName,
                                                                  out string? outputReg,
                                                                  out string? outputImage,
                                                                  out string? outputTag,
                                                                  out string? _outputDigest))
        {
            Log.LogError($"Could not parse {nameof(FullyQualifiedBaseImageName)}: {{0}}", FullyQualifiedBaseImageName);
            return !Log.HasLoggedErrors;
        }

        try
        {
            if (!ContainerHelpers.NormalizeImageName(ContainerImageName, out var normalizedImageName))
            {
                Log.LogMessage(null, KnownStrings.ErrorCodes.CONTAINER001, "Container.InvalidImageName", null, 0, 0, 0, 0, MessageImportance.High, "'{0}' was not a valid container image name, it was normalized to '{1}'", nameof(ContainerImageName), normalizedImageName);
                NewContainerImageName = normalizedImageName!; // known to be not null due to output of NormalizeImageName
            }
            else
            {
                // name was valid already
                NewContainerImageName = ContainerImageName;
            }
        }
        catch (ArgumentException)
        {
            Log.LogError($"Invalid {nameof(ContainerImageName)}: {{0}}", ContainerImageName);
            return !Log.HasLoggedErrors;
        }

        ParsedContainerRegistry = outputReg ?? "";
        ParsedContainerImage = outputImage ?? "";
        ParsedContainerTag = outputTag ?? "";
        NewContainerRegistry = ContainerRegistry;
        NewContainerTags = validTags;

        if (BuildEngine != null)
        {
            Log.LogMessage(MessageImportance.Low, "Parsed the following properties. Note: Spaces are replaced with dashes.");
            Log.LogMessage(MessageImportance.Low, "Host: {0}", ParsedContainerRegistry);
            Log.LogMessage(MessageImportance.Low, "Image: {0}", ParsedContainerImage);
            Log.LogMessage(MessageImportance.Low, "Tag: {0}", ParsedContainerTag);
            Log.LogMessage(MessageImportance.Low, "Image Name: {0}", NewContainerImageName);
            Log.LogMessage(MessageImportance.Low, "Image Tags: {0}", String.Join(", ", NewContainerTags));
        }

        return !Log.HasLoggedErrors;
    }

    public void ValidateEnvironmentVariables()
    {
        var filteredEnvVars = ContainerEnvironmentVariables.Where((x) => ContainerHelpers.IsValidEnvironmentVariable(x.ItemSpec)).ToArray<ITaskItem>();
        var badEnvVars = ContainerEnvironmentVariables.Where((x) => !ContainerHelpers.IsValidEnvironmentVariable(x.ItemSpec));

        foreach (var badEnvVar in badEnvVars)
        {
            if (BuildEngine != null)
            {
                Log.LogWarning($"{nameof(ContainerEnvironmentVariables)}: '{badEnvVar.ItemSpec}' was not a valid Environment Variable. Ignoring.");
            }
        }

        NewContainerEnvironmentVariables = new ITaskItem[filteredEnvVars.Length];

        for (int i = 0; i < filteredEnvVars.Length; i++)
        {
            NewContainerEnvironmentVariables[i] = filteredEnvVars[i];
        }
    }
}
