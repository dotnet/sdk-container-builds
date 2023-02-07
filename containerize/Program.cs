﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.NET.Build.Containers;
using System.CommandLine.Parsing;
using System.Text;

var publishDirectoryArg = new Argument<DirectoryInfo>(
    name: "PublishDirectory",
    description: "The directory for the build outputs to be published.")
    .LegalFilePathsOnly().ExistingOnly();

var baseRegistryOpt = new Option<string>(
    name: "--baseregistry",
    description: "The registry to use for the base image.")
{
    IsRequired = true
};

var baseImageNameOpt = new Option<string>(
    name: "--baseimagename",
    description: "The base image to pull.")
{
    IsRequired = true
};

var baseImageTagOpt = new Option<string>(
    name: "--baseimagetag",
    description: "The base image tag. Ex: 6.0",
    getDefaultValue: () => "latest");

var outputRegistryOpt = new Option<string>(
    name: "--outputregistry",
    description: "The registry to push to.")
{
    IsRequired = false
};

var imageNameOpt = new Option<string>(
    name: "--imagename",
    description: "The name of the output image that will be pushed to the registry.")
{
    IsRequired = true
};

var imageTagsOpt = new Option<string[]>(
    name: "--imagetags",
    description: "The tags to associate with the new image.")
{
    AllowMultipleArgumentsPerToken = true
};

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
    IsRequired = true,
    AllowMultipleArgumentsPerToken = true
};

var entrypointArgsOpt = new Option<string[]>(
    name: "--entrypointargs",
    description: "Arguments to pass alongside Entrypoint.")
{
    AllowMultipleArgumentsPerToken = true
};

var labelsOpt = new Option<string[]>(
    name: "--labels",
    description: "Labels that the image configuration will include in metadata.",
    parseArgument: result =>
    {
        var labels = result.Tokens.Select(x => x.Value).ToArray();
        var badLabels = labels.Where((v) => v.Split('=').Length != 2);

        // Is there a non-zero number of Labels that didn't split into two elements? If so, assume invalid input and error out
        if (badLabels.Any())
        {
            result.ErrorMessage = "Incorrectly formatted labels: " + badLabels.Aggregate((x, y) => x = x + ";" + y);

            return Array.Empty<string>();
        }
        return labels;
    })
{
    AllowMultipleArgumentsPerToken = true
};

var portsOpt = new Option<Port[]>(
    name: "--ports",
    description: "Ports that the application declares that it will use. Note that this means nothing to container hosts, by default - it's mostly documentation. Ports should be of the form {number}/{type}, where {type} is tcp or udp",
    parseArgument: result => {
        var ports = result.Tokens.Select(x => x.Value).ToArray();
        var goodPorts = new List<Port>();
        var badPorts = new List<(string, ContainerHelpers.ParsePortError)>();
        
        foreach (var port in ports) {
            var split = port.Split('/');
            if (split.Length != 2) {
                badPorts.Add((port, ContainerHelpers.ParsePortError.UnknownPortFormat));
                continue;
            }
            if (ContainerHelpers.TryParsePort(split[0], split[1], out var portInfo, out var portError)) {
                goodPorts.Add(portInfo);
            } else {
                var pe = (ContainerHelpers.ParsePortError)portError!;
                badPorts.Add((port, pe));
            }
        }

        if (badPorts.Count != 0)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Incorrectly formatted ports:");
            foreach (var (badPort, error) in badPorts){
                var errors = Enum.GetValues<ContainerHelpers.ParsePortError>().Where(e => error.HasFlag(e));
                builder.AppendLine($"\t{badPort}:\t({string.Join(", ", errors)})");
            }
            result.ErrorMessage = builder.ToString();
            return Array.Empty<Port>();
        }
        return goodPorts.ToArray();
    }
){
    AllowMultipleArgumentsPerToken = true
};

var envVarsOpt = new Option<string[]>(
    name: "--environmentvariables",
    description: "Container environment variables to set.",
    parseArgument: result =>
    {
        var envVars = result.Tokens.Select(x => x.Value).ToArray();
        var badEnvVars = envVars.Where((v) => v.Split('=').Length != 2);

        if (badEnvVars.Any())
        {
            result.ErrorMessage = "Incorrectly formatted environment variables: " + badEnvVars.Aggregate((x, y) => x = x + ";" + y);

            return Array.Empty<string>();
        }
        return envVars;
    })
{
    AllowMultipleArgumentsPerToken = true
};

var ridOpt = new Option<string>(name: "--rid", description: "Runtime Identifier of the generated container.");

var ridGraphPathOpt = new Option<string>(name: "--ridgraphpath", description: "Path to the RID graph file.");

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
    labelsOpt,
    portsOpt,
    envVarsOpt,
    ridOpt,
    ridGraphPathOpt
};

root.SetHandler(async (context) =>
{
    DirectoryInfo _publishDir = context.ParseResult.GetValueForArgument(publishDirectoryArg);
    string _baseReg = context.ParseResult.GetValueForOption(baseRegistryOpt) ?? "";
    string _baseName = context.ParseResult.GetValueForOption(baseImageNameOpt) ?? "";
    string _baseTag = context.ParseResult.GetValueForOption(baseImageTagOpt) ?? "";
    string? _outputReg = context.ParseResult.GetValueForOption(outputRegistryOpt);
    string _name = context.ParseResult.GetValueForOption(imageNameOpt) ?? "";
    string[] _tags = context.ParseResult.GetValueForOption(imageTagsOpt) ?? Array.Empty<string>();
    string _workingDir = context.ParseResult.GetValueForOption(workingDirectoryOpt) ?? "";
    string[] _entrypoint = context.ParseResult.GetValueForOption(entrypointOpt) ?? Array.Empty<string>();
    string[] _entrypointArgs = context.ParseResult.GetValueForOption(entrypointArgsOpt) ?? Array.Empty<string>();
    string[] _labels = context.ParseResult.GetValueForOption(labelsOpt) ?? Array.Empty<string>();
    Port[] _ports = context.ParseResult.GetValueForOption(portsOpt) ?? Array.Empty<Port>();
    string[] _envVars = context.ParseResult.GetValueForOption(envVarsOpt) ?? Array.Empty<string>();
    string _rid = context.ParseResult.GetValueForOption(ridOpt) ?? "";
    string _ridGraphPath = context.ParseResult.GetValueForOption(ridGraphPathOpt) ?? "";
    await ContainerBuilder.Containerize(_publishDir, _workingDir, _baseReg, _baseName, _baseTag, _entrypoint, _entrypointArgs, _name, _tags, _outputReg, _labels, _ports, _envVars, _rid, _ridGraphPath).ConfigureAwait(false);
});

return await root.InvokeAsync(args).ConfigureAwait(false);
