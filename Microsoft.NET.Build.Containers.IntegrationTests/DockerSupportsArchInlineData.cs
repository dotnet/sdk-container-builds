// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Json;
using Microsoft.DotNet.CommandUtils;
using Xunit.Sdk;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerSupportsArchInlineData : DataAttribute
{
    // an optimization - this doesn't change over time so we can compute it once
    private static string[] LinuxPlatforms = GetSupportedLinuxPlatforms();

    // another optimization - daemons don't switch types easily or quickly, so this is as good as static
    private static bool IsWindowsDaemon = GetIsWindowsDaemon();

    private readonly string _arch;
    private readonly object[] _data;

    public DockerSupportsArchInlineData(string arch, params object[] data)
    {
        _arch = arch;
        _data = data;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (DaemonSupportsArch(_arch))
        {
            return new object[][] { _data.Prepend(_arch).ToArray() };
        };
        return Array.Empty<object[]>();
    }

    private bool DaemonSupportsArch(string arch)
    {
        if (LinuxPlatforms.Contains(arch))
        {
            return true;
        }
        else
        {
            if (IsWindowsDaemon && arch.StartsWith("windows", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            base.Skip = $"Skipping test because Docker daemon does not support {arch}.";
            return false;
        }
    }

    private static string[] GetSupportedLinuxPlatforms()
    {
        var inspectResult = new BasicCommand(null, "docker", "buildx", "inspect", "default").Execute();
        inspectResult.Should().Pass();
        var platformsLine = inspectResult.StdOut!.Split(Environment.NewLine).First(x => x.StartsWith("Platforms:", StringComparison.OrdinalIgnoreCase));
        return platformsLine.Substring("Platforms: ".Length).Split(",", StringSplitOptions.TrimEntries);
    }

    private static bool GetIsWindowsDaemon()
    {
        // the config json has an OSType property that is either "linux" or "windows" -
        // we can't use this for linux arch detection because that isn't enough information.
        var config = LocalDocker.GetConfigAsync(sync: true, cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
        if (config.RootElement.TryGetProperty("OSType", out JsonElement osTypeProperty))
        {
            return osTypeProperty.GetString() == "windows";
        }
        else
        {
            return false;
        }
    }
}
