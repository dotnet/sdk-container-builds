using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
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
    /// The registry to push the new container to.
    /// </summary>
    [Required]
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

    public ParseContainerProperties()
    {
        FullyQualifiedBaseImageName = "";
        ContainerRegistry = "";
        ContainerImageName = "";
        ContainerImageTag = "";
        ContainerImageTags = Array.Empty<string>();
        ParsedContainerRegistry = "";
        ParsedContainerImage = "";
        ParsedContainerTag = "";
        NewContainerRegistry = "";
        NewContainerImageName = "";
        NewContainerTags = Array.Empty<string>();
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
        return invalidTags.Count() == 0;
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
                Log.LogError(null, "CONTAINER003", "Container.InvalidTag", null, 0, 0, 0, 0, $"Invalid {nameof(ContainerImageTag)} provided: {{0}}. Image tags must be alphanumeric, underscore, hyphen, or period.", ContainerImageTag);
            }
        }
        else if (ContainerImageTags.Length != 0 && !TryValidateTags(ContainerImageTags, out var valids, out var invalids))
        {
            validTags = valids;
            if (invalids.Any())
            {
                (string message, string args) = invalids switch
                {
                    { Length: 1 } => ($"Invalid {nameof(ContainerImageTags)} provided: {{0}}. {nameof(ContainerImageTags)} must be a semicolon-delimited list of valid image tags. Image tags must be alphanumeric, underscore, hyphen, or period.", invalids[0]),
                    _ => ($"Invalid {nameof(ContainerImageTags)} provided: {{0}}. {nameof(ContainerImageTags)} must be a semicolon-delimited list of valid image tags. Image tags must be alphanumeric, underscore, hyphen, or period.", String.Join(", ", invalids))
                };
                Log.LogError(null, "CONTAINER003", "Container.InvalidTag", null, 0, 0, 0, 0, message, args);
                return !Log.HasLoggedErrors;
            }
        }
        else
        {
            validTags = Array.Empty<string>();
        }

        string registryToUse = string.Empty;

        if (!ContainerRegistry.StartsWith("http://") &&
             !ContainerRegistry.StartsWith("https://") &&
             !ContainerRegistry.StartsWith("docker://"))
        {
            // Default to https when no scheme is present: https://github.com/distribution/distribution/blob/26163d82560f4dda94bd7b87d587f94644c5af79/reference/normalize.go#L88
            registryToUse = "https://";
        }

        registryToUse += ContainerRegistry;

        if (!ContainerHelpers.IsValidRegistry(registryToUse))
        {
            Log.LogError("Could not recognize registry '{0}'. Does your registry need a scheme, like 'https://'?", ContainerRegistry);
            return !Log.HasLoggedErrors;
        }

        if (FullyQualifiedBaseImageName.Contains(' ') && BuildEngine != null)
        {
            Log.LogWarning($"{nameof(FullyQualifiedBaseImageName)} had spaces in it, replacing with dashes.");
        }

        if (!ContainerHelpers.TryParseFullyQualifiedContainerName(FullyQualifiedBaseImageName.Replace(' ', '-'),
                                                                  out string? outputReg,
                                                                  out string? outputImage,
                                                                  out string? outputTag))
        {
            Log.LogError($"Could not parse {nameof(FullyQualifiedBaseImageName)}: {0}", FullyQualifiedBaseImageName);
            return !Log.HasLoggedErrors;
        }

        try
        {
            if (!ContainerHelpers.NormalizeImageName(ContainerImageName, out string? normalizedImageName))
            {
                Log.LogMessage(MessageImportance.High, $"{nameof(ContainerImageName)}:'{ContainerImageName}' was not a valid container image name, it was normalized to {normalizedImageName}");
                NewContainerImageName = normalizedImageName ?? "";
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
        NewContainerRegistry = registryToUse;
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
}
